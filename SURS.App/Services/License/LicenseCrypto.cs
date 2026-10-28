using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SURS.License
{
    /// <summary>
    /// 授权卡（License Card）领域模型、编解码与签名协议（版本 2）。
    ///
    /// 该文件会被多处同时编译（通过 &lt;Compile Link&gt; 复用同一份源文件，避免两端协议漂移）：
    ///   1) SURS.App              —— 软件验证端（只内置公钥）
    ///   2) SURS.LicenseTool      —— 甲方发卡端（持有私钥，绝不发布）
    ///   3) SURS.LicenseSelfTest  —— 协议/生命周期自检
    /// 因此本文件禁止引用任何 WPF / UI 类型，只允许使用 BCL。
    ///
    /// 语义（v2）：有效期采用“相对窗口”。
    ///   卡只声明“从本机首次激活当刻起可用 X 天”，不写死绝对日期；
    ///   首次激活的当刻由软件在该电脑上记录（firstUseUtc），
    ///   到期 = firstUseUtc + ValidityDays×24h。这是“经典试用版”语义。
    ///
    /// 卡号（激活卡）形态：
    ///   SURS2.&lt;base64url(规范UTF8字节)&gt;.&lt;base64url(RSA-SHA256签名)&gt;
    /// 签名覆盖“规范串”原始字节，任何字段被篡改都会导致验签失败。
    /// </summary>
    public sealed class LicensePayload
    {
        public const int SchemeVersion = 2;

        /// <summary>PBKDF2 盐长度（字节）。</summary>
        public const int SaltLength = 16;

        /// <summary>PBKDF2 派生密码散列长度（字节）。</summary>
        public const int HashLength = 32;

        /// <summary>PBKDF2 迭代次数（本场景足够，且登录/激活开销可接受）。</summary>
        public const int Pbkdf2Iterations = 100_000;

        /// <summary>有效性天数上限（防御性约束，避免异常签发）。</summary>
        public const int MaxValidityDays = 3650;

        /// <summary>机构备注（note）最大长度。</summary>
        public const int MaxNoteLength = 40;

        private static readonly Regex AccountPattern = new Regex(
            @"^[A-Za-z0-9._-]{1,32}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HashSet<char> ForbiddenNoteChars = new HashSet<char> { '\n', '\r', '=' };

        /// <summary>账号（仅允许安全字符，保证规范串无歧义）。</summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>密码盐（base64）。</summary>
        public string PasswordSaltBase64 { get; set; } = string.Empty;

        /// <summary>密码 PBKDF2 散列（base64）。</summary>
        public string PasswordHashBase64 { get; set; } = string.Empty;

        /// <summary>自首次激活当刻起可用的天数（相对窗口）。</summary>
        public int ValidityDays { get; set; }

        /// <summary>机构备注（可选，用于 60 家医院发卡识别/审计；可含中文）。</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>签发时间（UTC Tick）。</summary>
        public long IssuedAtUtcTicks { get; set; }

        /// <summary>账号是否满足安全字符约束。</summary>
        public bool IsAccountValid() => !string.IsNullOrEmpty(Account) && AccountPattern.IsMatch(Account);

        /// <summary>备注是否合法（无换行/等号/控制符，长度合规）。</summary>
        public bool IsNoteValid()
        {
            if (Note == null)
            {
                return false;
            }

            if (Note.Length > MaxNoteLength)
            {
                return false;
            }

            foreach (var ch in Note)
            {
                if (ForbiddenNoteChars.Contains(ch) || char.IsControl(ch))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将字段按固定顺序序列化为“规范串”。
        /// 签发端与验证端必须调用同一实现对同一数据得到完全一致的字节，验签才成立。
        /// </summary>
        public string ToCanonicalString()
        {
            return string.Join("\n", new[]
            {
                "v=" + SchemeVersion.ToString(CultureInfo.InvariantCulture),
                "account=" + Account,
                "salt=" + PasswordSaltBase64,
                "hash=" + PasswordHashBase64,
                "days=" + ValidityDays.ToString(CultureInfo.InvariantCulture),
                "note=" + (Note ?? string.Empty),
                "issued=" + IssuedAtUtcTicks.ToString(CultureInfo.InvariantCulture),
            });
        }

        public byte[] ToCanonicalBytes() => Encoding.UTF8.GetBytes(ToCanonicalString());

        /// <summary>
        /// 由甲方侧输入构造一张“未签名”的载荷（含盐与密码散列、基本信息校验）。
        /// 工具在拿到私钥后再对其签名生成卡号。
        /// </summary>
        public static LicensePayload Create(
            string account,
            string password,
            int days,
            string? note,
            DateTimeOffset issuedUtc)
        {
            if (string.IsNullOrEmpty(account))
            {
                throw new ArgumentException("账号不能为空。");
            }

            if (password == null)
            {
                throw new ArgumentException("密码不能为空。");
            }

            if (days < 1 || days > MaxValidityDays)
            {
                throw new ArgumentException("有效天数必须在 1~" + MaxValidityDays + " 之间。");
            }

            var payload = new LicensePayload
            {
                Account = account,
                ValidityDays = days,
                Note = (note ?? string.Empty).Trim(),
                IssuedAtUtcTicks = issuedUtc.UtcTicks,
            };

            if (!payload.IsAccountValid())
            {
                throw new ArgumentException("账号仅允许 1-32 位字母/数字/点/下划线/横线。");
            }

            if (!payload.IsNoteValid())
            {
                throw new ArgumentException("机构备注无效：长度不超过 40，且不能包含换行、等号或控制字符。");
            }

            var salt = LicenseCrypto.GenerateSalt();
            payload.PasswordSaltBase64 = Convert.ToBase64String(salt);
            payload.PasswordHashBase64 = LicenseCrypto.HashPasswordToBase64(password, salt);
            return payload;
        }

        /// <summary>解析规范串（不校验签名，只解析字段）。</summary>
        public static LicenseParseResult TryParseCanonical(string text, out LicensePayload? payload)
        {
            payload = null;
            if (string.IsNullOrEmpty(text))
            {
                return LicenseParseResult.Fail("授权数据为空");
            }

            var allowed = new HashSet<string>
            {
                "v", "account", "salt", "hash", "days", "note", "issued"
            };

            var fields = new Dictionary<string, string>();
            var lines = text.Split('\n');
            if (lines.Length == 0 || lines.Length > 32)
            {
                return LicenseParseResult.Fail("授权数据行数异常");
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    return LicenseParseResult.Fail("授权数据包含空行");
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    return LicenseParseResult.Fail("授权数据格式错误（缺少字段分隔）");
                }

                var key = line.Substring(0, eq);
                var value = line.Substring(eq + 1);

                if (!allowed.Contains(key))
                {
                    return LicenseParseResult.Fail("授权数据包含未知字段: " + key);
                }

                if (fields.ContainsKey(key))
                {
                    return LicenseParseResult.Fail("授权数据包含重复字段: " + key);
                }

                // 空值仅允许出现在 note（备注可为空），其余字段必须有值。
                if (value.Length == 0 && key != "note")
                {
                    return LicenseParseResult.Fail("授权数据字段为空: " + key);
                }

                fields[key] = value;
            }

            foreach (var key in allowed)
            {
                if (!fields.ContainsKey(key))
                {
                    return LicenseParseResult.Fail("授权数据缺少字段: " + key);
                }
            }

            if (fields["v"] != SchemeVersion.ToString(CultureInfo.InvariantCulture))
            {
                return LicenseParseResult.Fail("授权卡协议版本不受支持");
            }

            var parsed = new LicensePayload
            {
                Account = fields["account"],
                PasswordSaltBase64 = fields["salt"],
                PasswordHashBase64 = fields["hash"],
                Note = fields["note"]
            };

            if (!int.TryParse(fields["days"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ||
                days < 1 || days > MaxValidityDays)
            {
                return LicenseParseResult.Fail("授权卡有效天数无效");
            }

            parsed.ValidityDays = days;

            if (!IsValidTicks(fields["issued"], out var issuedTicks) || issuedTicks <= 0)
            {
                return LicenseParseResult.Fail("授权卡签发时间无效");
            }

            parsed.IssuedAtUtcTicks = issuedTicks;

            if (!parsed.IsAccountValid())
            {
                return LicenseParseResult.Fail("账号格式无效");
            }

            if (!parsed.IsNoteValid())
            {
                return LicenseParseResult.Fail("机构备注格式无效");
            }

            if (!TryDecodeFixedLength(fields["salt"], SaltLength, out var _))
            {
                return LicenseParseResult.Fail("密码盐无效");
            }

            if (!TryDecodeFixedLength(fields["hash"], HashLength, out var _))
            {
                return LicenseParseResult.Fail("密码散列无效");
            }

            payload = parsed;
            return LicenseParseResult.Ok();
        }

        private static bool IsValidTicks(string text, out long ticks)
        {
            ticks = 0;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
            {
                return false;
            }

            return ticks >= 0 && ticks <= DateTime.MaxValue.Ticks;
        }

        private static bool TryDecodeFixedLength(string base64, int expectedLength, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(base64))
            {
                return false;
            }

            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return false;
            }

            return bytes.Length == expectedLength;
        }
    }

    /// <summary>规范串解析结果。</summary>
    public sealed class LicenseParseResult
    {
        public bool Success { get; }
        public string? Error { get; }

        private LicenseParseResult(bool success, string? error)
        {
            Success = success;
            Error = error;
        }

        public static LicenseParseResult Ok() => new LicenseParseResult(true, null);
        public static LicenseParseResult Fail(string error) => new LicenseParseResult(false, error);
    }

    /// <summary>
    /// 授权卡签名 / 验签 / 密码散列等密码学操作与卡号工具方法。
    /// 签发端用私钥，验证端用公钥；base64url 与规范串由此处统一实现。
    /// </summary>
    public static class LicenseCrypto
    {
        /// <summary>卡号前缀（协议版本标识）。</summary>
        public const string CardPrefix = "SURS2";

        /// <summary>卡号长度上限，防止畸形输入造成资源滥用。</summary>
        public const int MaxCardLength = 8192;

        private static readonly HashAlgorithmName PasswordHashAlgorithm = HashAlgorithmName.SHA256;

        /// <summary>生成随机盐。</summary>
        public static byte[] GenerateSalt()
        {
            return RandomNumberGenerator.GetBytes(LicensePayload.SaltLength);
        }

        /// <summary>对明文密码计算 PBKDF2 散列。</summary>
        public static byte[] HashPassword(string password, byte[] salt)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (salt == null || salt.Length == 0)
            {
                throw new ArgumentException("盐不能为空", nameof(salt));
            }

            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                LicensePayload.Pbkdf2Iterations,
                PasswordHashAlgorithm,
                LicensePayload.HashLength);
        }

        public static string HashPasswordToBase64(string password, byte[] salt)
        {
            return Convert.ToBase64String(HashPassword(password, salt));
        }

        /// <summary>常量时间比较，避免时序侧信道。</summary>
        public static bool PasswordMatches(string password, byte[] salt, string expectedHashBase64)
        {
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(expectedHashBase64);
            }
            catch (FormatException)
            {
                return false;
            }

            if (expected.Length != LicensePayload.HashLength)
            {
                return false;
            }

            var actual = HashPassword(password, salt);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>去除卡号中的所有空白（还原被换行/空格截断粘贴的卡号）。</summary>
        public static string NormalizeCard(string card)
        {
            if (string.IsNullOrWhiteSpace(card))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(card.Length);
            foreach (var ch in card)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        /// <summary>输入是否为“授权串（长密码/授权卡）”样式（以 SURS2. 开头）。</summary>
        public static bool LooksLikeToken(string input)
        {
            var normalized = NormalizeCard(input ?? string.Empty);
            return normalized.StartsWith(CardPrefix + ".", StringComparison.Ordinal);
        }

        /// <summary>卡号指纹：SHA-256(规范化卡号) 十六进制。用于“单卡单机一次性”判定。</summary>
        public static string CardFingerprint(string normalizedCard)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCard ?? string.Empty));
            return Convert.ToHexString(bytes);
        }

        /// <summary>用私钥签发一张授权卡。</summary>
        public static string CreateCard(LicensePayload payload, RSA signingKey)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (signingKey == null)
            {
                throw new ArgumentNullException(nameof(signingKey));
            }

            var canonicalBytes = payload.ToCanonicalBytes();
            var signature = signingKey.SignData(
                canonicalBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return string.Format(
                "{0}.{1}.{2}",
                CardPrefix,
                Base64Url.Encode(canonicalBytes),
                Base64Url.Encode(signature));
        }

        /// <summary>用公钥验证授权卡签名，并返回载荷（无论签名是否有效都会尝试解析，便于给出准确提示）。</summary>
        public static LicenseCardCheck VerifyCard(string card, RSA publicKey)
        {
            var result = new LicenseCardCheck();

            var normalized = NormalizeCard(card);
            if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxCardLength)
            {
                result.Error = "激活卡号为空或超长";
                return result;
            }

            var parts = normalized.Split('.');
            if (parts.Length != 3 || parts[0] != CardPrefix)
            {
                result.Error = "激活卡号格式无效（应为 SURS2.xxx.yyy）";
                return result;
            }

            byte[] canonicalBytes;
            byte[] signature;
            try
            {
                canonicalBytes = Base64Url.Decode(parts[1]);
                signature = Base64Url.Decode(parts[2]);
            }
            catch (FormatException)
            {
                result.Error = "激活卡号编码无效";
                return result;
            }

            if (canonicalBytes.Length == 0 || canonicalBytes.Length > MaxCardLength || signature.Length == 0)
            {
                result.Error = "激活卡号内容为空";
                return result;
            }

            var text = Encoding.UTF8.GetString(canonicalBytes);
            var parse = LicensePayload.TryParseCanonical(text, out var payload);
            result.Parsed = parse.Success;
            result.Payload = payload;
            result.Error = parse.Error;

            result.SignatureValid = publicKey.VerifyData(
                canonicalBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!result.SignatureValid)
            {
                result.Error = "授权卡签名无效（内容可能被修改）";
            }

            return result;
        }

        /// <summary>仅解析卡号内容（不做签名校验），用于支持排查而无需私钥。</summary>
        public static LicenseCardCheck InspectCard(string card)
        {
            var result = new LicenseCardCheck();
            var normalized = NormalizeCard(card);
            if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxCardLength)
            {
                result.Error = "激活卡号为空或超长";
                return result;
            }

            var parts = normalized.Split('.');
            if (parts.Length != 3 || parts[0] != CardPrefix)
            {
                result.Error = "激活卡号格式无效（应为 SURS2.xxx.yyy）";
                return result;
            }

            byte[] canonicalBytes;
            try
            {
                canonicalBytes = Base64Url.Decode(parts[1]);
            }
            catch (FormatException)
            {
                result.Error = "激活卡号编码无效";
                return result;
            }

            var parse = LicensePayload.TryParseCanonical(
                Encoding.UTF8.GetString(canonicalBytes), out var payload);
            result.Parsed = parse.Success;
            result.Payload = payload;
            result.Error = result.Parsed ? null : parse.Error;
            return result;
        }
    }

    /// <summary>授权卡验签/解析的检查结果。</summary>
    public sealed class LicenseCardCheck
    {
        /// <summary>规范串能否被成功解析。</summary>
        public bool Parsed { get; set; }

        /// <summary>签名是否有效（公钥验签结果）。</summary>
        public bool SignatureValid { get; set; }

        /// <summary>解析出的载荷（解析失败时为 null）。</summary>
        public LicensePayload? Payload { get; set; }

        /// <summary>人类可读错误信息。</summary>
        public string? Error { get; set; }

        /// <summary>是否可被视为“完整有效的一张卡”。</summary>
        public bool IsFullyValid => Parsed && SignatureValid && Payload != null;
    }

    /// <summary>base64url（RFC 4648 §5）编解码。</summary>
    public static class Base64Url
    {
        public static string Encode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static byte[] Decode(string text)
        {
            var normalized = text.Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2: normalized += "=="; break;
                case 3: normalized += "="; break;
            }

            return Convert.FromBase64String(normalized);
        }
    }
}