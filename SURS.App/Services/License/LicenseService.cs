using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SURS.App.Common;
using SURS.License;

namespace SURS.App.Services.License
{
    /// <summary>授权状态枚举（认证与授权分离：账号密码只负责“认证”，状态负责“授权”）。</summary>
    public enum LicenseStatus
    {
        /// <summary>本机尚未安装有效授权卡（首次使用，需要激活/登录）。</summary>
        NotActivated = 0,

        /// <summary>授权有效，允许使用。</summary>
        Valid = 1,

        /// <summary>有效期已过，需要发放方发新卡续期。</summary>
        Expired = 2,

        /// <summary>卡号签名无效或内容被篡改。</summary>
        Tampered = 3,

        /// <summary>检测到系统时间被回拨。</summary>
        ClockRollback = 4,

        /// <summary>账号或密码不匹配。</summary>
        InvalidCredentials = 5,

        /// <summary>激活卡号本身无法解析（格式/内容错误，非篡改）。</summary>
        InvalidCard = 6,

        /// <summary>授权数据存储不可用（磁盘/权限/损坏）。</summary>
        StorageError = 7,
    }

    /// <summary>授权检查结果。</summary>
    public sealed class LicenseCheckResult
    {
        public LicenseStatus Status { get; init; }
        public LicensePayload? Payload { get; init; }
        public int? RemainingDays { get; init; }

        /// <summary>本机授权窗口的起始时刻（首次激活当刻，UTC）。</summary>
        public DateTimeOffset? FirstUseUtc { get; init; }

        /// <summary>本机授权窗口的到期时刻（UTC）。</summary>
        public DateTimeOffset? ValidUntilUtc { get; init; }

        /// <summary>可直接展示给用户的说明（中文）。</summary>
        public string Message { get; init; } = string.Empty;

        public bool IsValid => Status == LicenseStatus.Valid;

        public static LicenseCheckResult Of(
            LicenseStatus status,
            LicensePayload? payload = null,
            int? remainingDays = null,
            DateTimeOffset? firstUseUtc = null,
            DateTimeOffset? validUntilUtc = null,
            string? message = null)
        {
            return new LicenseCheckResult
            {
                Status = status,
                Payload = payload,
                RemainingDays = remainingDays,
                FirstUseUtc = firstUseUtc,
                ValidUntilUtc = validUntilUtc,
                Message = message ?? string.Empty,
            };
        }
    }

    /// <summary>
    /// 授权服务：负责“启动门禁 + 首次激活/续期 + 登录 + 防回拨水印 + 防重置 + 有效期判定 + 本地持久化”。
    ///
    /// 语义（v2，纯离线）：
    ///   1) 认证 ≠ 授权：账号密码只验证身份；能否运行由“本机授权状态”决定；
    ///   2) 相对窗口：到期 = 本机首次激活当刻(firstUseUtc) + 卡上 ValidityDays×24h；
    ///   3) 防篡改用签名：卡由发卡方私钥签发，软件只内置公钥验签；
    ///   4) 防改时间用水印：持久化“见过的最大时间”，当前时间更早即判定回拨；
    ///   5) 防重置（“首次使用起算”后的头号威胁）：
    ///      多副本（LocalAppData 主档 + ProgramData 影子 + HKCU 镜像）+ 生成代(seq) 恢复，
    ///      机器指纹(machineFingerprintProvider，默认 MachineGuid) 绑定，
    ///      单卡单机一次性（consumed 台账），有效窗口内一律拒重激活。
    ///
    /// 交付形态：UI 只有“账号 + 密码”。
    ///   A 形式：随包 license.card（医院无感，自动读取）＋ 短密码；
    ///   B 形式：把“授权串 SURS2.xxx”当作密码粘贴（单 exe 场景）。
    ///   到期后：允许进入登录页的“续期”通道，需输入甲方发的新卡（新 license.card 或新授权串）后方可进入。
    ///
    /// 诚实边界：本方案面向 L1 威胁（改时间、改配置、删数据、换卡蹭 7 天）；
    /// 重装整机/换新机/精心逆向无法由纯离线方案抵抗（属公认天花板）。
    /// </summary>
    public sealed class LicenseService
    {
        /// <summary>时钟容差：允许 5 分钟内的正常时钟偏差，避免误判。</summary>
        public const int ClockToleranceSeconds = 5 * 60;

        /// <summary>授权卡文件名（随发布包交付；默认随 exe 同一目录）。</summary>
        public const string CardFileName = "license.card";

        private const string StateFileName = "license.state";
        private const int StateSchemeVersion = 2;
        private const string RegistrySubKey = @"Software\SURS\License";
        private const string RegistryStateValue = "State";

        private readonly ILogger? _logger;
        private readonly string _storageDir;
        private readonly string _shadowDir;
        private readonly Func<DateTimeOffset> _nowUtc;
        private readonly bool _useRegistryMirror;
        private readonly Func<string> _machineFingerprintProvider;
        private readonly RSA _verifier;

        private bool _loaded;
        private string? _installedCard;
        private LicensePayload? _activePayload;
        private string? _machineFp;

        /// <summary>本机授权数据主目录（默认 %LocalAppData%\SURS\License；测试可注入其它目录）。</summary>
        public string StoragePath => _storageDir;

        public LicenseService(
            ILogger? logger = null,
            string? storageDirectory = null,
            Func<DateTimeOffset>? nowUtc = null,
            bool useRegistryMirror = true,
            string? shadowDirectory = null,
            Func<string>? machineFingerprintProvider = null)
        {
            _logger = logger;
            _storageDir = storageDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SURS", "License");
            _shadowDir = shadowDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SURS", "License");
            _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
            _useRegistryMirror = useRegistryMirror;
            _machineFingerprintProvider = machineFingerprintProvider ?? MachineFingerprint.Compute;
            _verifier = EmbeddedLicenseKey.CreateVerifierRsa();
        }

        /// <summary>是否已安装有效授权卡（本机存储里有卡）。</summary>
        public bool HasActiveCard => _installedCard != null;

        /// <summary>已安装卡的账号（用于回填账号输入框）。</summary>
        public string? InstalledAccount => _activePayload?.Account;

        /// <summary>当前有效载荷（仅当状态 Valid 时可靠）。</summary>
        public LicensePayload? ActivePayload => _activePayload;

        /// <summary>输入是否为“授权串”样式（以 SURS2. 开头）。</summary>
        public static bool LooksLikeToken(string input) => LicenseCrypto.LooksLikeToken(input);

        /// <summary>从指定路径读取授权卡内容（规范化后返回；不存在/异常返回 null）。</summary>
        public static string? ReadCardFileOrDefault(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return null;
                }

                var normalized = LicenseCrypto.NormalizeCard(File.ReadAllText(path));
                return string.IsNullOrEmpty(normalized) ? null : normalized;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ============================================================
        // 对外入口
        // ============================================================

        /// <summary>
        /// 启动门禁：每次启动先执行。
        /// 返回 NotActivated / Valid / Expired 时应用可进入登录页
        /// （Expired 表示进入“续期”通道，需新卡才可放行）；
        /// 其余状态一律阻止运行并展示 <see cref="LicenseCheckResult.Message"/>。
        /// </summary>
        public LicenseCheckResult StartupGate()
        {
            EnsureLoaded();
            if (_installedCard == null)
            {
                return LicenseCheckResult.Of(
                    LicenseStatus.NotActivated,
                    message: "本机尚未激活。请将发布文件夹放到程序目录后重新启动；或在下方直接输入发放方提供的账号、密码完成激活。");
            }

            // 1) 验签（防篡改）
            var check = LicenseCrypto.VerifyCard(_installedCard, _verifier);
            if (!check.SignatureValid || check.Payload == null)
            {
                _logger?.LogWarning("授权卡签名校验失败（内容可能被修改）。");
                return LicenseCheckResult.Of(
                    LicenseStatus.Tampered,
                    message: "授权信息无效或已被修改，软件已停止使用。请联系发放方处理。");
            }

            _activePayload = check.Payload;

            // 2) 有效期窗口 + 水印 + 多副本一致性
            var status = EvaluateCurrentWindow(out var current);
            if (status != LicenseStatus.Valid)
            {
                return GateStatusResult(status, current);
            }

            return ValidResult(check.Payload, current!);
        }        /// <summary>
        /// 激活/续期：用一张“新授权”在本机开窗。
        /// 输入的 <paramref name="cardCode"/> 可以是随包 license.card 读出的卡号，也可以是“授权串”（B 形式）。
        /// </summary>
        public LicenseCheckResult TryActivate(string cardCode)
        {
            var code = LicenseCrypto.NormalizeCard(cardCode);
            if (string.IsNullOrEmpty(code))
            {
                return LicenseCheckResult.Of(LicenseStatus.InvalidCard, message: "未提供有效的授权信息。");
            }

            var check = LicenseCrypto.VerifyCard(code, _verifier);
            if (check.Payload == null)
            {
                return LicenseCheckResult.Of(
                    LicenseStatus.InvalidCard,
                    message: "授权信息无法识别，请核对账号/密码，或确认授权文件完整。");
            }

            if (!check.SignatureValid)
            {
                _logger?.LogWarning("授权卡签名校验失败：" + (check.Error ?? string.Empty));
                return LicenseCheckResult.Of(
                    LicenseStatus.Tampered,
                    message: "授权信息无效或已被修改，请联系发放方核发新授权。");
            }

            var payload = check.Payload;
            DateTimeOffset now = _nowUtc();
            var machineFp = GetMachineFingerprint();

            // —— 读取所有可读副本，执行“防重置”检查 ——
            long maxSeq = long.MinValue;
            var survivors = new List<CopyState>();
            foreach (var copy in ReadAllCopies())
            {
                if (copy is { Corrupt: false, Exists: true })
                {
                    survivors.Add(copy);
                }
            }

            if (survivors.Count > 0)
            {
                // 主档序列号低于其它副本 => 有人回放了旧主档。
                var primary = FindPrimaryState();
                if (primary is { Exists: true, Corrupt: false })
                {
                    long maxSurvivorSeq = long.MinValue;
                    foreach (var s in survivors) if (s.Seq > maxSurvivorSeq) maxSurvivorSeq = s.Seq;
                    if (primary.Seq < maxSurvivorSeq)
                    {
                        _logger?.LogWarning("授权主档与其它副本序列不一致（可能被回放）。");
                        return LicenseCheckResult.Of(
                            LicenseStatus.Tampered,
                            message: "授权状态存在冲突，软件已停止使用。请联系发放方处理。");
                    }
                }

                // 累计台账与活性判断（跨副本、跨代）。
                var cardFp = LicenseCrypto.CardFingerprint(code);
                var consumedAll = new HashSet<string>(StringComparer.Ordinal);
                bool liveOnThisMachine = false;
                long wmMax = long.MinValue;
                foreach (var s in survivors)
                {
                    foreach (var c in s.ConsumedCards) consumedAll.Add(c);
                    if (s.LastSeenUtcTicks > wmMax) wmMax = s.LastSeenUtcTicks;
                    if (s.DeviceFp == machineFp && s.LastExpiryUtcTicks > now.UtcTicks)
                    {
                        liveOnThisMachine = true;
                    }

                    if (s.Seq > maxSeq) maxSeq = s.Seq;
                }

                // 规则①（含跨副本/跨代）：本机存在未到期的授权记录 => 一律拒绝重激活。
                if (liveOnThisMachine)
                {
                    _logger?.LogWarning("检测到本机存在未到期的授权使用记录，拒绝重激活。");
                    return LicenseCheckResult.Of(
                        LicenseStatus.Tampered,
                        message: "本机存在未到期的授权使用记录，无法重复激活。若需续期，请到期后使用发放方提供的新授权。");
                }

                // 规则②：单卡单机一次性（台账含历史累计）。
                if (consumedAll.Contains(cardFp))
                {
                    return LicenseCheckResult.Of(
                        LicenseStatus.Tampered,
                        message: "该授权卡已在本机使用过，无法重复激活。请向发放方领取新授权。");
                }

                // 回拨连续性：激活也必须尊重历史水印，防止“删状态回拨”绕过。
                if (wmMax > 0 && now.UtcTicks < wmMax - ToleranceTicks())
                {
                    _logger?.LogWarning("激活时检测到系统时间回拨，拒绝激活。");
                    return LicenseCheckResult.Of(
                        LicenseStatus.ClockRollback,
                        message: "检测到系统时间异常，无法激活。请校准系统时间后重试。");
                }
            }

            // —— 防重置检查通过：开新窗口并写全部副本 ——
            long newFirstUse = now.UtcTicks;
            long newLastExpiry = now.AddHours((double)payload.ValidityDays * 24.0).UtcTicks;
            if (newLastExpiry > DateTime.MaxValue.Ticks)
            {
                newLastExpiry = DateTime.MaxValue.Ticks;
            }

            var consumedBefore = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in survivors)
            {
                foreach (var c in s.ConsumedCards) consumedBefore.Add(c);
            }

            var newConsumed = consumedBefore;
            newConsumed.Add(LicenseCrypto.CardFingerprint(code));

            long nextSeq = maxSeq == long.MinValue ? 1L : checked(maxSeq + 1);
            var record = new CopyState
            {
                Generation = Guid.NewGuid().ToString("N"),
                DeviceFp = machineFp,
                FirstUseUtcTicks = newFirstUse,
                LastSeenUtcTicks = now.UtcTicks,
                LastExpiryUtcTicks = newLastExpiry,
                Seq = nextSeq,
                ConsumedCards = newConsumed,
            };

            // 持久化顺序：主卡 → 主状态 → 影子 → 注册表；主档两步失败则回滚，保持“卡⇒状态”不变式。
            if (!SafeWriteText(Path.Combine(_storageDir, CardFileName), code) ||
                !SafeWriteState(record))
            {
                SafeDeleteIfExists(Path.Combine(_storageDir, CardFileName));
                SafeDeleteIfExists(StatePath());
                _logger?.LogError("激活持久化失败：无法写入授权数据。");
                return LicenseCheckResult.Of(
                    LicenseStatus.StorageError,
                    message: "授权数据存储不可用，请联系管理员检查磁盘权限。");
            }

            TryWriteShadowState(record);
            if (_useRegistryMirror)
            {
                WriteRegistryState(record);
            }

            _installedCard = code;
            _activePayload = payload;
            _loaded = true;
            _logger?.LogInfo($"激活成功：{payload.Account}（{payload.Note}）");

            return ValidResult(payload, record);
        }

        /// <summary>
        /// 登录校验：账号 + 密码（或授权串）。
        /// 每次登录会重新验签、复查有效期、水印与多副本（纵深防御）。
        /// 账号或密码任一不匹配统一返回 <see cref="LicenseStatus.InvalidCredentials"/>，不透露细节。
        /// </summary>
        public LicenseCheckResult CheckLogin(string account, string password)
        {
            EnsureLoaded();
            if (_installedCard == null)
            {
                return LicenseCheckResult.Of(
                    LicenseStatus.NotActivated, message: "本机尚未激活，请先完成首次激活。");
            }

            var check = LicenseCrypto.VerifyCard(_installedCard, _verifier);
            if (!check.SignatureValid || check.Payload == null)
            {
                return LicenseCheckResult.Of(
                    LicenseStatus.Tampered,
                    message: "授权信息无效或已被修改，软件已停止使用。");
            }

            var payload = check.Payload;

            var status = EvaluateCurrentWindow(out var current);
            if (status != LicenseStatus.Valid)
            {
                return StatusResult(status, payload, current);
            }

            if (!string.Equals(account, payload.Account, StringComparison.Ordinal))
            {
                _logger?.LogWarning("登录失败：账号不匹配。");
                return LicenseCheckResult.Of(
                    LicenseStatus.InvalidCredentials, message: "账号或密码错误。");
            }

            bool passwordOk;
            if (LicenseCrypto.LooksLikeToken(password ?? string.Empty))
            {
                // B 形式：密码即授权串 => 与本机已装卡逐字比对。
                var tokenNorm = LicenseCrypto.NormalizeCard(password ?? string.Empty);
                passwordOk = string.Equals(tokenNorm, _installedCard, StringComparison.Ordinal);
            }
            else
            {
                // A 形式：短密码 => PBKDF2 校验。
                var salt = SafeDecode(payload.PasswordSaltBase64);
                passwordOk = salt != null &&
                             LicenseCrypto.PasswordMatches(password ?? string.Empty, salt, payload.PasswordHashBase64);
            }

            if (!passwordOk)
            {
                _logger?.LogWarning("登录失败：密码不匹配。");
                return LicenseCheckResult.Of(
                    LicenseStatus.InvalidCredentials, message: "账号或密码错误。");
            }

            _activePayload = payload;
            return ValidResult(payload, current!);
        }

        // ============================================================
        // 窗口/水印/多副本核心
        // ============================================================

        /// <summary>
        /// 读取全部状态副本并统一为“当前窗口记录”（主档优先，缺失时由影子/镜像恢复）。
        /// 通过校验：有效期未过、时钟未被回拨、各副本序列一致；随后更新水印（上次见到时间）。
        /// 返回 Valid 时 <paramref name="current"/> 为当前记录；否则为应阻止的状态。
        /// </summary>
        private LicenseStatus EvaluateCurrentWindow(out CopyState? current)
        {
            current = null;

            var primary = FindPrimaryState();
            if (primary is { Corrupt: true })
            {
                _logger?.LogWarning("授权主状态损坏（可能被修改）。");
                return LicenseStatus.Tampered;
            }

            var shadow = FindShadowState();
            var registry = _useRegistryMirror ? FindRegistryState() : null;

            var survivors = new List<CopyState>();
            if (primary is { Corrupt: false, Exists: true }) survivors.Add(primary);
            if (shadow is { Corrupt: false, Exists: true }) survivors.Add(shadow);
            if (registry is { Corrupt: false, Exists: true }) survivors.Add(registry);

            if (survivors.Count == 0)
            {
                // 卡在而状态被全部清空 => 判定为绕过尝试。
                _logger?.LogWarning("授权卡存在但状态被清空。");
                return LicenseStatus.Tampered;
            }

            long maxSeq = long.MinValue;
            foreach (var s in survivors) if (s.Seq > maxSeq) maxSeq = s.Seq;

            if (primary is { Exists: true, Corrupt: false } && primary.Seq < maxSeq)
            {
                // 主档被回放成更旧版本 => 冲突。
                _logger?.LogWarning("授权主档与其它副本序列不一致（可能被回放）。");
                return LicenseStatus.Tampered;
            }

            CopyState best = survivors[0];
            foreach (var s in survivors) if (s.Seq > best.Seq) best = s;

            if (best.FirstUseUtcTicks <= 0 || best.LastExpiryUtcTicks <= 0)
            {
                _logger?.LogWarning("授权窗口时间字段异常。");
                return LicenseStatus.Tampered;
            }

            // 主档缺失但存在同“代”的备份 => 由回写步骤恢复主档（窗口保持不变）。
            if (primary is { Exists: true, Corrupt: false } == false)
            {
                _logger?.LogWarning("授权主档缺失，已由备份恢复到主目录（窗口保持不变）。");
            }

            DateTimeOffset now = _nowUtc();

            // 过期
            if (now.UtcTicks >= best.LastExpiryUtcTicks)
            {
                return LicenseStatus.Expired;
            }

            // 回拨水印：取所有副本（含历史）中最大的 lastSeen。
            long wmMax = long.MinValue;
            foreach (var s in survivors) if (s.LastSeenUtcTicks > wmMax) wmMax = s.LastSeenUtcTicks;
            if (wmMax > 0 && now.UtcTicks < wmMax - ToleranceTicks())
            {
                _logger?.LogWarning("检测到系统时间回拨。");
                return LicenseStatus.ClockRollback;
            }

            // 回写水印（含主档恢复）：以“当前记录”为底，仅更新 lastSeen/seq。
            long nextLastSeen = Math.Max(now.UtcTicks, wmMax);
            long nextSeq = checked(maxSeq + 1);
            var updated = CloneForWrite(best, nextSeq, nextLastSeen);

            if (!SafeWriteState(updated))
            {
                return LicenseStatus.StorageError;
            }

            TryWriteShadowState(updated);
            if (_useRegistryMirror)
            {
                WriteRegistryState(updated);
            }

            current = best;
            return LicenseStatus.Valid;
        }

        private LicenseCheckResult GateStatusResult(LicenseStatus status, CopyState? current)
        {
            return status switch
            {
                LicenseStatus.Expired => LicenseCheckResult.Of(
                    LicenseStatus.Expired,
                    _activePayload,
                    firstUseUtc: FromTicks(current?.FirstUseUtcTicks ?? 0),
                    validUntilUtc: FromTicks(current?.LastExpiryUtcTicks ?? 0),
                    message: current != null
                        ? ExpiredMessage(current, _activePayload)
                        : "本软件授权已到期，软件已停止使用。如需继续使用，请联系发放方领取新的授权。"),
                LicenseStatus.ClockRollback => LicenseCheckResult.Of(
                    LicenseStatus.ClockRollback, message: "检测到系统时间异常（可能被回拨），软件已停止使用。请校准系统时间后重试。"),
                LicenseStatus.StorageError => LicenseCheckResult.Of(
                    LicenseStatus.StorageError, message: "授权数据存储不可用，请联系管理员检查磁盘权限。"),
                _ => LicenseCheckResult.Of(
                    LicenseStatus.Tampered, message: "授权信息无效或已被修改，软件已停止使用。请联系发放方处理。"),
            };
        }

        private LicenseCheckResult StatusResult(LicenseStatus status, LicensePayload payload, CopyState? current)
        {
            if (status == LicenseStatus.Expired)
            {
                return LicenseCheckResult.Of(
                    LicenseStatus.Expired,
                    payload,
                    firstUseUtc: FromTicks(current?.FirstUseUtcTicks ?? 0),
                    validUntilUtc: FromTicks(current?.LastExpiryUtcTicks ?? 0),
                    message: current != null ? ExpiredMessage(current, payload) : "本软件授权已到期。");
            }

            return LicenseCheckResult.Of(
                status,
                payload,
                message: status == LicenseStatus.ClockRollback
                    ? "检测到系统时间异常（可能被回拨），软件已停止使用。请校准系统时间后重试。"
                    : "授权状态异常，软件已停止使用。请联系发放方处理。");
        }

        private LicenseCheckResult ValidResult(LicensePayload payload, CopyState record)
        {
            var first = FromTicks(record.FirstUseUtcTicks);
            var until = FromTicks(record.LastExpiryUtcTicks);
            int remaining = RemainingDays(until, _nowUtc());
            return LicenseCheckResult.Of(
                LicenseStatus.Valid,
                payload,
                remainingDays: remaining,
                firstUseUtc: first,
                validUntilUtc: until,
                message: $"本授权自 {FormatLocal(first)} 起 {payload.ValidityDays} 天，至 {FormatLocal(until)} 到期，剩余 {remaining} 天。");
        }

        private static string ExpiredMessage(CopyState record, LicensePayload? payload)
        {
            var until = FromTicks(record.LastExpiryUtcTicks);
            var note = string.IsNullOrWhiteSpace(payload?.Note) ? string.Empty : $"（{payload!.Note}）";
            return $"本软件授权已于 {FormatLocal(until)} 到期{note}，软件已停止使用。如需继续使用，请联系发放方领取新的授权。";
        }

        private static int RemainingDays(DateTimeOffset until, DateTimeOffset now)
        {
            var days = Math.Ceiling((until - now).TotalDays);
            return Math.Max(0, (int)days);
        }

        private static DateTimeOffset FromTicks(long ticks)
        {
            return new DateTimeOffset(Math.Max(0, ticks), TimeSpan.Zero);
        }

        private static string FormatLocal(DateTimeOffset utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private long ToleranceTicks()
        {
            return ClockToleranceSeconds * TimeSpan.TicksPerSecond;
        }

        private string GetMachineFingerprint()
        {
            if (_machineFp == null)
            {
                try
                {
                    _machineFp = _machineFingerprintProvider() ?? string.Empty;
                }
                catch (Exception)
                {
                    _machineFp = string.Empty;
                }
            }

            return _machineFp;
        }        // ============================================================
        // 持久化（主档 + 影子 + 注册表镜像）
        // ============================================================

        private static readonly object RegistryTreeLock = new object();

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            var content = TryReadAllText(Path.Combine(_storageDir, CardFileName));
            if (string.IsNullOrWhiteSpace(content))
            {
                _installedCard = null;
                return;
            }

            _installedCard = LicenseCrypto.NormalizeCard(content);
            _activePayload = null;
        }

        private string StatePath() => Path.Combine(_storageDir, StateFileName);
        private string ShadowStatePath() => Path.Combine(_shadowDir, StateFileName);

        private CopyState? FindPrimaryState() => ReadFileState(StatePath());
        private CopyState? FindShadowState() => ReadFileState(ShadowStatePath());

        private IEnumerable<CopyState?> ReadAllCopies()
        {
            yield return FindPrimaryState();
            yield return FindShadowState();
            if (_useRegistryMirror)
            {
                yield return FindRegistryState();
            }
        }

        private CopyState? ReadFileState(string path)
        {
            if (!File.Exists(path))
            {
                return new CopyState { Exists = false };
            }

            string? text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                return new CopyState { Exists = true, Corrupt = true };
            }

            var state = new CopyState { Exists = true };
            if (!TryParseStateText(text, state))
            {
                state.Corrupt = true;
            }

            return state;
        }

        private bool SafeWriteState(CopyState record)
        {
            try
            {
                Directory.CreateDirectory(_storageDir);
                var content = SerializeState(record);
                WriteAllTextAtomic(StatePath(), content);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void TryWriteShadowState(CopyState record)
        {
            try
            {
                Directory.CreateDirectory(_shadowDir);
                var content = SerializeState(record);
                WriteAllTextAtomic(ShadowStatePath(), content);
            }
            catch (Exception)
            {
                // 影子写失败仅降低冗余度，不影响主流程。
            }
        }

        private bool SafeWriteText(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteAllTextAtomic(path, content);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteAllTextAtomic(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            File.Move(tmp, path, overwrite: true);
        }

        private static void SafeDeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // 忽略清理失败
            }
        }

        private static string? TryReadAllText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[]? SafeDecode(string base64)
        {
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // ============================================================
        // 状态文本（v=2）序列化/解析
        // ============================================================

        private sealed class CopyState
        {
            public bool Exists;
            public bool Corrupt;

            public string Generation = string.Empty;
            public string DeviceFp = string.Empty;
            public long FirstUseUtcTicks;
            public long LastSeenUtcTicks;
            public long LastExpiryUtcTicks;
            public long Seq;
            public HashSet<string> ConsumedCards = new HashSet<string>(StringComparer.Ordinal);
        }

        private static CopyState CloneForWrite(CopyState source, long seq, long lastSeen)
        {
            return new CopyState
            {
                Exists = true,
                Generation = source.Generation,
                DeviceFp = source.DeviceFp,
                FirstUseUtcTicks = source.FirstUseUtcTicks,
                LastSeenUtcTicks = lastSeen,
                LastExpiryUtcTicks = source.LastExpiryUtcTicks,
                Seq = seq,
                ConsumedCards = new HashSet<string>(source.ConsumedCards, StringComparer.Ordinal),
            };
        }

        private static string SerializeState(CopyState s)
        {
            var consumed = new StringBuilder();
            bool first = true;
            foreach (var fp in s.ConsumedCards)
            {
                if (!first) consumed.Append(',');
                consumed.Append(fp);
                first = false;
            }

            return string.Join("\n", new[]
            {
                "v=" + StateSchemeVersion.ToString(CultureInfo.InvariantCulture),
                "gen=" + s.Generation,
                "fp=" + s.DeviceFp,
                "firstUse=" + s.FirstUseUtcTicks.ToString(CultureInfo.InvariantCulture),
                "lastSeen=" + s.LastSeenUtcTicks.ToString(CultureInfo.InvariantCulture),
                "lastExpiry=" + s.LastExpiryUtcTicks.ToString(CultureInfo.InvariantCulture),
                "seq=" + s.Seq.ToString(CultureInfo.InvariantCulture),
                "consumed=" + consumed,
            });
        }

        private static bool TryParseStateText(string text, CopyState target)
        {
            var allowed = new HashSet<string>
            {
                "v", "gen", "fp", "firstUse", "lastSeen", "lastExpiry", "seq", "consumed"
            };

            var fields = new Dictionary<string, string>();
            foreach (var line in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    return false;
                }

                var key = line.Substring(0, eq);
                var value = line.Substring(eq + 1);
                if (!allowed.Contains(key) || fields.ContainsKey(key) || value.Length == 0)
                {
                    return false;
                }

                fields[key] = value;
            }

            if (!fields.TryGetValue("v", out var v) || v != StateSchemeVersion.ToString(CultureInfo.InvariantCulture))
            {
                return false;
            }

            if (!fields.TryGetValue("gen", out var gen) || string.IsNullOrEmpty(gen) ||
                !fields.TryGetValue("fp", out var fp) || string.IsNullOrEmpty(fp) ||
                !fields.TryGetValue("firstUse", out var firstUse) ||
                !long.TryParse(firstUse, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstUseTicks) ||
                !fields.TryGetValue("lastSeen", out var lastSeen) ||
                !long.TryParse(lastSeen, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastSeenTicks) ||
                !fields.TryGetValue("lastExpiry", out var lastExpiry) ||
                !long.TryParse(lastExpiry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastExpiryTicks) ||
                !fields.TryGetValue("seq", out var seqRaw) ||
                !long.TryParse(seqRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq))
            {
                return false;
            }

            if (firstUseTicks < 0 || lastSeenTicks < 0 || lastExpiryTicks < 0 || seq < 0)
            {
                return false;
            }

            target.Generation = gen;
            target.DeviceFp = fp;
            target.FirstUseUtcTicks = firstUseTicks;
            target.LastSeenUtcTicks = lastSeenTicks;
            target.LastExpiryUtcTicks = lastExpiryTicks;
            target.Seq = seq;

            if (fields.TryGetValue("consumed", out var consumedRaw) && consumedRaw.Length > 0)
            {
                foreach (var piece in consumedRaw.Split(','))
                {
                    var trimmed = piece.Trim();
                    if (trimmed.Length > 0)
                    {
                        target.ConsumedCards.Add(trimmed);
                    }
                }
            }

            return true;
        }

        // ============================================================
        // 注册表镜像（HKCU，无需管理员；失败视为可忽略的加固项）
        // ============================================================

        private CopyState? FindRegistryState()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                if (key == null)
                {
                    return null;
                }

                var raw = key.GetValue(RegistryStateValue) as string;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var state = new CopyState { Exists = true };
                if (!TryParseStateText(raw, state))
                {
                    // 镜像损坏按不存在处理（主档仍可工作）。
                    return null;
                }

                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void WriteRegistryState(CopyState record)
        {
            try
            {
                lock (RegistryTreeLock)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                    key?.SetValue(RegistryStateValue, SerializeState(record), RegistryValueKind.String);
                }
            }
            catch (Exception)
            {
                // 注册表镜像写入失败不影响主流程（仅降低冗余度）。
            }
        }
    }
}