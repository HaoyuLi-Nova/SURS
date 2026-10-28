using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SURS.License;

namespace SURS.LicenseTool
{
    /// <summary>
    /// SURS 授权卡发放工具（仅供甲方 / 授权管理员使用，绝不随软件发布）。
    ///
    /// 命令：
    ///   init       生成 RSA 密钥对（私钥保存在 keys-dir，公钥需嵌入软件 EmbeddedLicenseKey.cs）
    ///   issue      签发一张“机构-账号-密码-有效天数”授权卡（= 激活卡 / 授权串）
    ///   pubkey     打印公钥（用于嵌入软件）
    ///   verify     用公钥验签某张卡（支持排查）
    ///   inspect    不验签，仅解析卡片内容（售后排查用，无需私钥）
    ///   ledger     发放台账查询（list 子命令）
    ///
    /// 语义（v2，首次使用起算）：
    ///   卡只声明“自本机首次激活当刻起可用 N 天”，到期 = 首次激活 + N×24h；
    ///   软件在该电脑上记录首次激活时刻，并做防重置（多副本 + 机器指纹 + 单卡单机一次性）。
    ///
    /// 安全红线：
    ///   1) keys-dir 中的 private_key.pem 是最高机密：离线保管、加密备份、严禁入库/发布；
    ///   2) 重新 init 会让旧卡全部失效，请谨慎；
    ///   3) 台账（ledger.jsonl）含明文账号密码，请单独妥善保管。
    /// </summary>
    internal static class Program
    {
        private const int DefaultKeyBits = 2048;
        private const int DefaultDays = 7;
        private const string CardFileName = "license.card";

        private static readonly string DefaultKeysDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SURS.LicenseTool", ".keys");

        private static readonly string DefaultLedgerPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SURS.LicenseTool", "ledger.jsonl");

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("错误：" + ex.Message);
                return 2;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "init": return Init(args);
                case "issue": return Issue(args);
                case "pubkey": return PubKey(args);
                case "verify": return Verify(args);
                case "inspect": return Inspect(args);
                case "ledger":
                case "list":
                    return Ledger(args);
                case "help":
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                default:
                    Console.Error.WriteLine("未知命令：" + args[0]);
                    PrintHelp();
                    return 2;
            }
        }

        // ------------------------------------------------------------
        // 参数解析辅助
        // ------------------------------------------------------------

        private static string? FindValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static bool HasFlag(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string KeysDir(string[] args)
        {
            var dir = FindValue(args, "--keys-dir");
            return string.IsNullOrWhiteSpace(dir) ? DefaultKeysDir : Path.GetFullPath(dir);
        }

        private static string LedgerPath(string[] args)
        {
            var path = FindValue(args, "--ledger");
            return string.IsNullOrWhiteSpace(path) ? DefaultLedgerPath : Path.GetFullPath(path);
        }

        private static string RequireArg(string[] args, string key)
        {
            var value = FindValue(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"缺少必填参数 {key}。");
            }

            return value;
        }

        private static int ParseInt(string[] args, string key, int defaultValue)
        {
            var value = FindValue(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                throw new ArgumentException($"{key} 必须是大于 0 的整数。");
            }

            return parsed;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("SURS 授权卡发放工具（60 家医院 · 每家 1 组账号密码 · 首次使用起算）");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  SURS.LicenseTool init    [--keys-dir DIR] [--bits 2048] [--force]");
            Console.WriteLine("  SURS.LicenseTool issue   --account 账号 [--password 密码|--gen-password] [--days 7] [--note 机构]");
            Console.WriteLine("                            [--license-file 发布包\\license.card] [--slip 交付单.txt] [--ledger 台账] [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool pubkey  [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool verify  <卡片号> [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool inspect <卡片号>");
            Console.WriteLine("  SURS.LicenseTool ledger  list [--note 过滤] [--account 过滤] [--full] [--ledger 台账]");
            Console.WriteLine();
            Console.WriteLine("命令说明：");
            Console.WriteLine("  init    生成 RSA 密钥对；私钥保存于 keys-dir，公钥需嵌入软件 EmbeddedLicenseKey.cs");
            Console.WriteLine("  issue   签发授权卡（激活卡/授权串）；默认 7 天，--days 可改，--note 记机构单位");
            Console.WriteLine("  pubkey  打印公钥 PEM（嵌入软件时用）");
            Console.WriteLine("  verify  用 keys-dir 的公钥验签卡片");
            Console.WriteLine("  inspect 不验签，仅解析卡片内容（售后排查用，无需私钥）");
            Console.WriteLine("  ledger list  查看发放台账（谁领了卡、何时签发、有效期）");
            Console.WriteLine();
            Console.WriteLine("默认 keys-dir：" + DefaultKeysDir);
            Console.WriteLine("默认台账：   " + DefaultLedgerPath);
            Console.WriteLine();
            Console.WriteLine("安全提醒：private_key.pem 严禁入库/发布；台账含明文密码，请妥善保管。");
        }

        // ------------------------------------------------------------
        // init
        // ------------------------------------------------------------

        private static int Init(string[] args)
        {
            var keysDir = KeysDir(args);
            var bitsRaw = FindValue(args, "--bits");
            int bits = DefaultKeyBits;
            if (!string.IsNullOrWhiteSpace(bitsRaw) &&
                (!int.TryParse(bitsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out bits) || bits < 1024))
            {
                throw new ArgumentException("--bits 必须是 >= 1024 的整数。");
            }

            var privatePath = Path.Combine(keysDir, "private_key.pem");
            var publicPath = Path.Combine(keysDir, "public_key.pem");

            if (!HasFlag(args, "--force") && File.Exists(privatePath))
            {
                throw new ArgumentException(
                    $"密钥已存在（{privatePath}）。如需重新生成请加 --force（注意：旧卡将全部失效）。");
            }

            Directory.CreateDirectory(keysDir);
            using var rsa = RSA.Create(bits);

            var privatePem = rsa.ExportPkcs8PrivateKeyPem();
            var publicPem = rsa.ExportSubjectPublicKeyInfoPem();

            File.WriteAllText(privatePath, privatePem, new UTF8Encoding(false));
            File.WriteAllText(publicPath, publicPem, new UTF8Encoding(false));

            Console.WriteLine("已生成 RSA-{0} 密钥对：", bits);
            Console.WriteLine("  私钥：" + privatePath);
            Console.WriteLine("  公钥：" + publicPath);
            Console.WriteLine();
            Console.WriteLine("下一步：把公钥粘贴到 SURS.App/Services/License/EmbeddedLicenseKey.cs 的 PublicKeyPem 并重新编译。");
            Console.WriteLine();
            Console.WriteLine("公开部分（公钥 PEM）：");
            Console.WriteLine(publicPem);
            Console.WriteLine();
            Console.WriteLine("!!! 私钥 private_key.pem 是授权系统运转的关键：请离线保管、加密备份，严禁发布会 !!!");
            return 0;
        }

        // ------------------------------------------------------------
        // issue
        // ------------------------------------------------------------

        private static int Issue(string[] args)
        {
            var account = RequireArg(args, "--account");
            var password = HasFlag(args, "--gen-password") ? GeneratePassword() : RequireArg(args, "--password");
            var days = ParseInt(args, "--days", DefaultDays);
            var note = FindValue(args, "--note") ?? string.Empty;
            var keysDir = KeysDir(args);
            var ledger = Path.GetFullPath(LedgerPath(args));

            var payload = LicensePayload.Create(account, password, days, note, DateTimeOffset.UtcNow);
            if (!payload.IsAccountValid())
            {
                throw new ArgumentException("账号仅允许 1-32 位字母/数字/点/下划线/横线。");
            }

            using var rsa = LoadPrivateRsa(keysDir);
            var card = LicenseCrypto.CreateCard(payload, rsa);
            var cardFp = LicenseCrypto.CardFingerprint(card);

            // 1) 先记台账（失败则中止，保证发放全程留痕）。
            var entry = new LedgerEntry
            {
                Seq = NextSeq(ledger) + 1,
                IssuedUtc = DateTimeOffset.UtcNow,
                Note = note.Trim(),
                Account = account,
                Days = days,
                Password = password,
                Card = card,
                CardFingerprint = cardFp,
            };

            var ledgerOk = AppendLedger(ledger, entry);
            if (!ledgerOk)
            {
                Console.Error.WriteLine("错误：无法写入发放台账：" + ledger);
                return 2;
            }

            // 2) 授权文件（license.card）
            var licenseFile = FindValue(args, "--license-file");
            if (!string.IsNullOrWhiteSpace(licenseFile))
            {
                var full = Path.GetFullPath(licenseFile);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, card + Environment.NewLine, new UTF8Encoding(false));
                Console.WriteLine("已写入授权文件：" + full);
            }

            // 3) 交付单（可选）
            var slip = FindValue(args, "--slip");
            if (!string.IsNullOrWhiteSpace(slip))
            {
                var full = Path.GetFullPath(slip);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, BuildSlipText(entry), new UTF8Encoding(false));
                Console.WriteLine("已写入交付单：" + full);
            }

            Console.WriteLine();
            Console.WriteLine("========== 授权信息 ==========");
            Console.WriteLine("机构：" + (entry.Note ?? string.Empty));
            Console.WriteLine("账号：" + entry.Account);
            Console.WriteLine("密码：" + entry.Password);
            Console.WriteLine("有效：" + entry.Days + " 天（自该院电脑首次激活当刻起算，共 " + (entry.Days * 24) + " 小时）");
            Console.WriteLine("台账序号：" + entry.Seq);
            Console.WriteLine();
            Console.WriteLine("========== 激活卡号 ==========");
            Console.WriteLine(entry.Card);
            Console.WriteLine();
            Console.WriteLine("提示：把“激活卡号 + 账号 + 密码”交给医院即可。");
            Console.WriteLine("提醒：台账（" + ledger + "）含明文密码，请勿外传。");
            return 0;
        }

        private static string BuildSlipText(LedgerEntry e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================================");
            sb.AppendLine("        SURS 软件授权交付单");
            sb.AppendLine("==============================================");
            sb.AppendLine("机构：" + (e.Note ?? string.Empty));
            sb.AppendLine("账号：" + e.Account);
            sb.AppendLine("密码：" + e.Password);
            sb.AppendLine("有效天数：" + e.Days + " 天（" + (e.Days * 24) + " 小时）");
            sb.AppendLine("签发时间(UTC)：" + e.IssuedUtc.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("使用说明：");
            sb.AppendLine("  1) 把“发布文件夹”（含授权文件）放到电脑上，双击 SURS 程序；");
            sb.AppendLine("  2) 首次使用输入上面的“账号 + 密码”即可激活（自动读取授权文件）；");
            sb.AppendLine("  3) 有效期自该电脑首次激活当刻起算 7 天，到期后请联系发放方领取新的授权；");
            sb.AppendLine("  4) 若授权文件缺失，可在“密码”位置直接粘贴下面的激活卡号完成激活。");
            sb.AppendLine();
            sb.AppendLine("激活卡号（授权串，备用/续期）：");
            sb.AppendLine(e.Card);
            sb.AppendLine();
            sb.AppendLine("请妥善保管本交付单，勿外传。");
            sb.AppendLine("==============================================");
            return sb.ToString();
        }

        // ------------------------------------------------------------
        // 台账（ledger * / list）
        // ------------------------------------------------------------

        private class LedgerEntry
        {
            public long Seq;
            public DateTimeOffset IssuedUtc;
            public string Note = string.Empty;
            public string Account = string.Empty;
            public int Days;
            public string Password = string.Empty;
            public string Card = string.Empty;
            public string CardFingerprint = string.Empty;
        }

        private static long NextSeq(string ledgerPath)
        {
            long max = 0;
            foreach (var entry in ReadLedger(ledgerPath))
            {
                if (entry.Seq > max) max = entry.Seq;
            }

            return max;
        }

        private static bool AppendLedger(string ledgerPath, LedgerEntry entry)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!);
                var sb = new StringBuilder();
                sb.Append("[record]").Append('\n');
                sb.Append("seq=").Append(entry.Seq.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("issuedUtc=").Append(entry.IssuedUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("note=").Append(entry.Note).Append('\n');
                sb.Append("account=").Append(entry.Account).Append('\n');
                sb.Append("days=").Append(entry.Days.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("passwordB64=").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Password))).Append('\n');
                sb.Append("card=").Append(entry.Card).Append('\n');
                sb.Append("cardFingerprint=").Append(entry.CardFingerprint).Append('\n');
                sb.Append('\n');

                File.AppendAllText(ledgerPath, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<LedgerEntry> ReadLedger(string ledgerPath)
        {
            var result = new List<LedgerEntry>();
            if (!File.Exists(ledgerPath))
            {
                return result;
            }

            LedgerEntry? current = null;
            foreach (var rawLine in File.ReadAllLines(ledgerPath))
            {
                var line = rawLine.TrimEnd('\r');
                if (line == "[record]")
                {
                    current = new LedgerEntry();
                    result.Add(current);
                    continue;
                }

                if (current == null || line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, eq);
                var value = line.Substring(eq + 1);
                switch (key)
                {
                    case "seq":
                        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq)) current.Seq = seq;
                        break;
                    case "issuedUtc":
                        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ) current.IssuedUtc = d;
                        break;
                    case "note": current.Note = value; break;
                    case "account": current.Account = value; break;
                    case "days":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)) current.Days = days;
                        break;
                    case "passwordB64":
                        try { current.Password = Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
                        catch (FormatException) { current.Password = value; }
                        break;
                    case "card": current.Card = value; break;
                    case "cardFingerprint": current.CardFingerprint = value; break;
                }
            }

            return result;
        }

        private static int Ledger(string[] args)
        {
            if (args.Length < 2 || !string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("用法：SURS.LicenseTool ledger list [--note 过滤] [--account 过滤] [--full] [--ledger 台账]");
                return 1;
            }

            var ledger = LedgerPath(args);
            var noteFilter = FindValue(args, "--note");
            var accountFilter = FindValue(args, "--account");
            bool full = HasFlag(args, "--full");

            var entries = ReadLedger(ledger);
            if (entries.Count == 0)
            {
                Console.WriteLine("（暂无发放记录：" + ledger + "）");
                return 0;
            }

            Console.WriteLine("发放台账（" + ledger + "）共 " + entries.Count + " 条：");
            Console.WriteLine();
            foreach (var e in entries)
            {
                if (!string.IsNullOrWhiteSpace(noteFilter) && !(e.Note.Contains(noteFilter, StringComparison.OrdinalIgnoreCase))) continue;
                if (!string.IsNullOrWhiteSpace(accountFilter) && !(e.Account.Contains(accountFilter, StringComparison.OrdinalIgnoreCase))) continue;

                Console.WriteLine("序号 {0} | {1} | 账号 {2} | {3} 天 | 签发(UTC) {4} | 密码 {5}",
                    e.Seq, string.IsNullOrEmpty(e.Note) ? "（未填机构）" : e.Note,
                    e.Account, e.Days, e.IssuedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), e.Password);
                if (full)
                {
                    Console.WriteLine("       卡指纹：" + e.CardFingerprint);
                    Console.WriteLine("       卡号：" + e.Card);
                }
            }

            return 0;
        }

        // ------------------------------------------------------------
        // pubkey / verify / inspect
        // ------------------------------------------------------------

        private static int PubKey(string[] args)
        {
            var publicPath = Path.Combine(KeysDir(args), "public_key.pem");
            if (!File.Exists(publicPath))
            {
                Console.WriteLine("未找到公钥文件：" + publicPath);
                Console.WriteLine("请先运行 init 生成密钥对。");
                return 1;
            }

            Console.WriteLine(File.ReadAllText(publicPath));
            return 0;
        }

        private static int Verify(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("请提供要校验的激活卡号。");
            }

            var card = args[1];
            var publicPath = Path.Combine(KeysDir(args), "public_key.pem");
            if (!File.Exists(publicPath))
            {
                Console.WriteLine("未找到公钥文件：" + publicPath);
                Console.WriteLine("请先运行 init 生成密钥对。");
                return 1;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(publicPath));

            var check = LicenseCrypto.VerifyCard(card, rsa);
            PrintCardCheck(check);
            return check.SignatureValid && check.Parsed ? 0 : 1;
        }

        private static int Inspect(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("请提供要解析的激活卡号。");
            }

            var check = LicenseCrypto.InspectCard(args[1]);
            PrintCardCheck(check);
            return check.Parsed ? 0 : 1;
        }

        private static void PrintCardCheck(LicenseCardCheck check)
        {
            Console.WriteLine("解析成功：" + (check.Parsed ? "是" : "否"));
            Console.WriteLine("签名有效：" + (check.SignatureValid ? "是" : "否"));
            if (check.Payload != null)
            {
                var p = check.Payload;
                Console.WriteLine("账号：" + p.Account);
                Console.WriteLine("机构：" + (string.IsNullOrEmpty(p.Note) ? "（未填）" : p.Note));
                Console.WriteLine("有效天数：" + p.ValidityDays + " 天（自首次激活起算）");
                Console.WriteLine("签发时间(本地)：" + FormatLocal(new DateTimeOffset(p.IssuedAtUtcTicks, TimeSpan.Zero)));
            }

            if (!string.IsNullOrEmpty(check.Error))
            {
                Console.WriteLine("提示：" + check.Error);
            }
        }

        private static string FormatLocal(DateTimeOffset utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static RSA LoadPrivateRsa(string keysDir)
        {
            var privatePath = Path.Combine(keysDir, "private_key.pem");
            if (!File.Exists(privatePath))
            {
                throw new ArgumentException("未找到私钥：" + privatePath + "。请先运行 init 生成密钥对。");
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(privatePath));
            return rsa;
        }

        // ------------------------------------------------------------
        // 密码生成
        // ------------------------------------------------------------

        private static readonly char[] GenLower = "abcdefghjkmnpqrstuvwxyz".ToCharArray();
        private static readonly char[] GenUpper = "ABCDEFGHJKMNPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] GenDigit = "23456789".ToCharArray();
        private static readonly char[] GenSymbol = "!@#$%^&*".ToCharArray();

        private static string GeneratePassword()
        {
            // 16 位，避开易混字符(0/O/1/l/I)，至少含大小写/数字/符号。
            const int length = 16;
            var pool = new List<char>(GenLower);
            pool.AddRange(GenUpper);
            pool.AddRange(GenDigit);
            pool.AddRange(GenSymbol);

            var chars = new char[length];
            chars[0] = GenLower[RandomNumberGenerator.GetInt32(GenLower.Length)];
            chars[1] = GenUpper[RandomNumberGenerator.GetInt32(GenUpper.Length)];
            chars[2] = GenDigit[RandomNumberGenerator.GetInt32(GenDigit.Length)];
            chars[3] = GenSymbol[RandomNumberGenerator.GetInt32(GenSymbol.Length)];
            for (int i = 4; i < length; i++)
            {
                chars[i] = pool[RandomNumberGenerator.GetInt32(pool.Count)];
            }

            // Fisher-Yates 洗牌
            for (int i = length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
    }
}