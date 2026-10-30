using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SURS.License;

namespace SURS.LicenseTool
{
    /// <summary>
    /// SURS 离线发卡工具。仅供甲方授权管理员使用，绝不随客户端发布。
    /// 命令：init / issue / batch / pubkey / verify / inspect / ledger list / list。
    /// </summary>
    internal static class Program
    {
        private const int DefaultKeyBits = 2048;
        private const int DefaultDays = 7;
        private const string CardFileName = "license.card";

        private static readonly string DefaultKeysDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SURS.LicenseTool", ".keys");

        private static readonly string DefaultLedgerPath = LicenseLedger.DefaultPath;

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
                case "batch": return Batch(args);
                case "pubkey": return PubKey(args);
                case "verify": return Verify(args);
                case "inspect": return Inspect(args);
                case "list": return ListLedger(args);
                case "ledger":
                    if (args.Length >= 2 && string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
                    {
                        return ListLedger(args);
                    }
                    Console.WriteLine("用法：SURS.LicenseTool ledger list [--note 过滤] [--account 过滤] [--full] [--ledger 台账]");
                    return 1;
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
            return args.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
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
                throw new ArgumentException("缺少必填参数 " + key + "。");
            }

            return value.Trim();
        }

        private static int ParseInt(string[] args, string key, int defaultValue)
        {
            var value = FindValue(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < 1 || parsed > LicensePayload.MaxValidityDays)
            {
                throw new ArgumentException(key + " 必须是 1~" + LicensePayload.MaxValidityDays + " 的整数。");
            }

            return parsed;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("SURS 离线授权卡发放工具（60 家医院 · 每家 1 组账号密码 · 首次使用起算）");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  SURS.LicenseTool init    [--keys-dir DIR] [--bits 2048] [--force]");
            Console.WriteLine("  SURS.LicenseTool issue   --note 机构 [--days 7] [--account hos01]");
            Console.WriteLine("                            [--password 密码 | --gen-password] [--out 交付单.txt]");
            Console.WriteLine("                            [--license-file 仅卡号文件.txt] [--ledger 台账.jsonl] [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool batch   --count N | --hospitals 清单.txt [--days 7] [--out 交付目录]");
            Console.WriteLine("                            [--password 通用密码] [--app-exe 发布\\SURS.App.exe]");
            Console.WriteLine("                            [--ledger 台账.jsonl] [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool pubkey  [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool verify  <激活卡号> [--keys-dir DIR]");
            Console.WriteLine("  SURS.LicenseTool inspect <激活卡号>");
            Console.WriteLine("  SURS.LicenseTool ledger  list [--note 过滤] [--account 过滤] [--full] [--ledger 台账]");
            Console.WriteLine("  SURS.LicenseTool list    [--note 过滤] [--account 过滤] [--full] [--ledger 台账]");
            Console.WriteLine();
            Console.WriteLine("默认行为：");
            Console.WriteLine("  issue 的 --note 必填；未指定 --account 时按台账自动分配 hosNN；");
            Console.WriteLine("  未指定 --password 或使用 --gen-password 时生成 16 位强随机密码；");
            Console.WriteLine("  每次成功签发自动追加 JSONL 台账（含明文密码，必须保密）。");
            Console.WriteLine();
            Console.WriteLine("默认 keys-dir：" + DefaultKeysDir);
            Console.WriteLine("默认台账：   " + DefaultLedgerPath);
            Console.WriteLine();
            Console.WriteLine("安全提醒：private_key.pem 严禁入库/发布；台账和交付单含明文密码，请妥善保管。");
        }

        private static int Init(string[] args)
        {
            var keysDir = KeysDir(args);
            var bitsRaw = FindValue(args, "--bits");
            int bits = DefaultKeyBits;
            if (!string.IsNullOrWhiteSpace(bitsRaw) &&
                (!int.TryParse(bitsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out bits) || bits < 2048))
            {
                throw new ArgumentException("--bits 必须是 >= 2048 的整数。");
            }

            var privatePath = Path.Combine(keysDir, "private_key.pem");
            var publicPath = Path.Combine(keysDir, "public_key.pem");
            if (!HasFlag(args, "--force") && File.Exists(privatePath))
            {
                throw new ArgumentException(
                    "密钥已存在（" + privatePath + "）。如需重新生成请加 --force（旧卡将全部失效）。");
            }

            Directory.CreateDirectory(keysDir);
            using var rsa = RSA.Create(bits);
            File.WriteAllText(privatePath, rsa.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
            File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem(), new UTF8Encoding(false));

            Console.WriteLine("已生成 RSA-" + bits + " 密钥对：");
            Console.WriteLine("  私钥：" + privatePath);
            Console.WriteLine("  公钥：" + publicPath);
            Console.WriteLine();
            Console.WriteLine("下一步：把公钥替换到 SURS.App/Services/License/EmbeddedLicenseKey.cs 后重新发布。");
            Console.WriteLine();
            Console.WriteLine("!!! private_key.pem 是最高机密：离线保管、加密备份，严禁发布或提交仓库 !!!");
            return 0;
        }
        // ------------------------------------------------------------
        // issue：接一家发一家
        // ------------------------------------------------------------

        private static int Issue(string[] args)
        {
            var note = RequireArg(args, "--note");
            var days = ParseInt(args, "--days", DefaultDays);
            var ledger = LedgerPath(args);
            var records = LicenseLedger.Load(ledger);
            var usedAccounts = new HashSet<string>(records.Select(r => r.Account), StringComparer.OrdinalIgnoreCase);

            var account = FindValue(args, "--account")?.Trim();
            if (string.IsNullOrEmpty(account))
            {
                account = NextAvailableDefaultAccount(records, usedAccounts);
            }

            var explicitPassword = FindValue(args, "--password");
            var forceGenerated = HasFlag(args, "--gen-password");
            if (forceGenerated && !string.IsNullOrWhiteSpace(explicitPassword))
            {
                throw new ArgumentException("--password 与 --gen-password 不能同时使用。");
            }

            var password = forceGenerated || string.IsNullOrWhiteSpace(explicitPassword)
                ? GeneratePassword()
                : explicitPassword;

            var issuedAt = DateTimeOffset.UtcNow;
            using var rsa = LoadPrivateRsa(KeysDir(args));
            var signed = CreateSignedCard(note, account, password, days, issuedAt, rsa);

            // 先落台账，确保任何已打印/交付的卡都有签发记录。
            LicenseLedger.Append(ledger, signed.Record);

            var outputPath = FindValue(args, "--out") ?? FindValue(args, "--slip");
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                var full = Path.GetFullPath(outputPath);
                EnsureParentDirectory(full);
                WriteTextAtomic(full, BuildDeliverySlip(signed.Record, signed.Card));
                Console.WriteLine("交付单：" + full);
            }

            var cardFilePath = FindValue(args, "--license-file");
            if (!string.IsNullOrWhiteSpace(cardFilePath))
            {
                var full = Path.GetFullPath(cardFilePath);
                EnsureParentDirectory(full);
                WriteTextAtomic(full, signed.Card + Environment.NewLine);
                Console.WriteLine("卡号文件：" + full);
            }

            Console.WriteLine();
            Console.WriteLine("签发成功");
            Console.WriteLine("  机构：" + signed.Record.Institution);
            Console.WriteLine("  账号：" + signed.Record.Account);
            Console.WriteLine("  密码：" + signed.Record.Password);
            Console.WriteLine("  天数：" + signed.Record.Days + " 天（自首次激活当刻起算）");
            Console.WriteLine("  卡指纹：" + signed.Record.CardFingerprint);
            Console.WriteLine("  激活卡号：" + signed.Card);
            Console.WriteLine();
            Console.WriteLine("台账：" + ledger);
            Console.WriteLine("注意：台账与交付单包含明文密码，仅限授权管理员保管。");
            return 0;
        }

        // ------------------------------------------------------------
        // batch：按医院清单批量交付（可选增强能力）
        // ------------------------------------------------------------

        private static int Batch(string[] args)
        {
            var hospitalsFile = FindValue(args, "--hospitals");
            var prefix = FindValue(args, "--prefix") ?? "医院";
            var days = ParseInt(args, "--days", DefaultDays);
            var outDir = Path.GetFullPath(FindValue(args, "--out") ??
                Path.Combine(Environment.CurrentDirectory, "交付"));
            var appExeRaw = FindValue(args, "--app-exe");
            var sharedPassword = FindValue(args, "--password");
            var ledger = LedgerPath(args);

            var hospitals = new List<(string Note, string? Account, string? Password)>();
            if (!string.IsNullOrWhiteSpace(hospitalsFile))
            {
                var file = Path.GetFullPath(hospitalsFile);
                if (!File.Exists(file))
                {
                    throw new ArgumentException("找不到机构清单文件：" + file);
                }

                int lineNo = 0;
                foreach (var raw in File.ReadAllLines(file))
                {
                    lineNo++;
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fields = line.Split(new[] { ',', '\t' });
                    var note = fields[0].Trim();
                    string? account = fields.Length >= 2 && !string.IsNullOrWhiteSpace(fields[1]) ? fields[1].Trim() : null;
                    string? password = fields.Length >= 3 && !string.IsNullOrWhiteSpace(fields[2]) ? fields[2].Trim() : null;
                    if (note.Length == 0)
                    {
                        throw new ArgumentException(file + " 第 " + lineNo + " 行缺少机构名。");
                    }

                    hospitals.Add((note, account, password));
                }

                if (hospitals.Count == 0)
                {
                    throw new ArgumentException("机构清单文件为空：" + file);
                }
            }
            else
            {
                int count = ParseInt(args, "--count", 0);
                if (count < 1)
                {
                    throw new ArgumentException("必须提供 --count N 或 --hospitals 机构清单文件。");
                }

                int width = count >= 100 ? 3 : 2;
                for (int i = 1; i <= count; i++)
                {
                    hospitals.Add((prefix + i.ToString("D" + width, CultureInfo.InvariantCulture), null, null));
                }
            }

            string? appExeFull = null;
            if (!string.IsNullOrWhiteSpace(appExeRaw))
            {
                appExeFull = Path.GetFullPath(appExeRaw);
                if (!File.Exists(appExeFull))
                {
                    throw new ArgumentException("找不到 --app-exe：" + appExeFull);
                }
            }

            Directory.CreateDirectory(outDir);
            var records = LicenseLedger.Load(ledger);
            var usedAccounts = new HashSet<string>(records.Select(r => r.Account), StringComparer.OrdinalIgnoreCase);
            var manifest = new List<string> { "序号,机构,账号,密码,有效天数,文件夹,激活卡号" };
            int created = 0;
            int skipped = 0;

            using var rsa = LoadPrivateRsa(KeysDir(args));
            for (int index = 1; index <= hospitals.Count; index++)
            {
                var hospital = hospitals[index - 1];
                var folderName = index.ToString("D2", CultureInfo.InvariantCulture) + "-" + SanitizeFolderName(hospital.Note);
                var folder = Path.Combine(outDir, folderName);
                if (Directory.Exists(folder))
                {
                    Console.WriteLine("跳过（文件夹已存在）：" + folderName);
                    skipped++;
                    continue;
                }

                var account = hospital.Account;
                if (string.IsNullOrEmpty(account))
                {
                    account = NextAvailableDefaultAccount(records, usedAccounts);
                }
                else if (usedAccounts.Contains(account))
                {
                    throw new ArgumentException("批量签发账号重复：" + account);
                }

                var password = !string.IsNullOrWhiteSpace(hospital.Password)
                    ? hospital.Password
                    : (!string.IsNullOrWhiteSpace(sharedPassword) ? sharedPassword : GeneratePassword());
                var issuedAt = DateTimeOffset.UtcNow;
                var signed = CreateSignedCard(hospital.Note, account, password, days, issuedAt, rsa);

                LicenseLedger.Append(ledger, signed.Record);
                records.Add(signed.Record);
                usedAccounts.Add(account);

                Directory.CreateDirectory(folder);
                WriteTextAtomic(Path.Combine(folder, CardFileName), signed.Card + Environment.NewLine);
                WriteTextAtomic(Path.Combine(folder, "交付单.txt"), BuildDeliverySlip(signed.Record, signed.Card));
                if (appExeFull != null)
                {
                    File.Copy(appExeFull, Path.Combine(folder, Path.GetFileName(appExeFull)), overwrite: false);
                }

                manifest.Add(string.Join(",",
                    index.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(signed.Record.Institution),
                    EscapeCsv(signed.Record.Account),
                    EscapeCsv(signed.Record.Password),
                    signed.Record.Days.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(folderName),
                    EscapeCsv(signed.Card)));
                Console.WriteLine("已生成 #" + index + "：" + folderName + "（账号 " + account + "）");
                created++;
            }

            if (created > 0)
            {
                WriteTextAtomic(Path.Combine(outDir, "交付清单.csv"), string.Join(Environment.NewLine, manifest) + Environment.NewLine);
            }

            Console.WriteLine();
            Console.WriteLine("批量完成：新建 " + created + " 家，跳过 " + skipped + " 家。");
            Console.WriteLine("台账：" + ledger);
            if (created > 0)
            {
                Console.WriteLine("提醒：台账、交付清单和每家交付单均含明文密码，请勿外传。");
            }
            return 0;
        }

        private static (LicenseLedgerRecord Record, string Card) CreateSignedCard(
            string note,
            string account,
            string password,
            int days,
            DateTimeOffset issuedAt,
            RSA signingKey)
        {
            var payload = LicensePayload.Create(account, password, days, note, issuedAt);
            var card = LicenseCrypto.CreateCard(payload, signingKey);
            var record = new LicenseLedgerRecord
            {
                Institution = payload.Note,
                Account = payload.Account,
                Days = payload.ValidityDays,
                IssuedAt = issuedAt,
                CardFingerprint = LicenseCrypto.CardFingerprint(card),
                Password = password,
                Warning = LicenseLedgerRecord.PlaintextWarning,
            };
            return (record, card);
        }

        private static string NextAvailableDefaultAccount(
            IEnumerable<LicenseLedgerRecord> records,
            HashSet<string> usedAccounts)
        {
            var list = records.ToList();
            var candidate = LicenseLedger.NextDefaultAccount(list);
            int number = int.Parse(candidate.Substring(3), CultureInfo.InvariantCulture);
            while (usedAccounts.Contains(candidate))
            {
                number++;
                candidate = "hos" + number.ToString("00", CultureInfo.InvariantCulture);
            }

            return candidate;
        }

        private static string BuildDeliverySlip(LicenseLedgerRecord record, string card)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================================");
            sb.AppendLine("              SURS 授权交付单");
            sb.AppendLine("==============================================");
            sb.AppendLine("机构：" + record.Institution);
            sb.AppendLine("账号：" + record.Account);
            sb.AppendLine("密码：" + record.Password);
            sb.AppendLine("有效天数：" + record.Days + " 天（" + (record.Days * 24) + " 小时）");
            sb.AppendLine("签发时间(UTC)：" + record.IssuedAt.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("卡指纹：" + record.CardFingerprint);
            sb.AppendLine();
            sb.AppendLine("使用说明：");
            sb.AppendLine("  1) 启动 SURS，在首次激活页面输入本单的激活卡号、账号和密码；");
            sb.AppendLine("  2) 授权自该电脑首次激活当刻起算 " + record.Days + " 天，到期自动停用；");
            sb.AppendLine("  3) 之后每次启动都只需输入账号和密码，软件不访问外网；");
            sb.AppendLine("  4) 同一张卡可在不同电脑分别激活，但在同一台电脑只能使用一次；");
            sb.AppendLine("  5) 到期续期必须联系发放方领取新卡，旧卡不能复用；");
            sb.AppendLine("  6) 请勿修改本单或外传账号密码，妥善保管。");
            sb.AppendLine();
            sb.AppendLine("激活卡号：");
            sb.AppendLine(card);
            sb.AppendLine();
            sb.AppendLine("注意保密：本交付单包含明文密码。");
            sb.AppendLine("==============================================");
            return sb.ToString();
        }
        // ------------------------------------------------------------
        // ledger list / list
        // ------------------------------------------------------------

        private static int ListLedger(string[] args)
        {
            var ledger = LedgerPath(args);
            var noteFilter = FindValue(args, "--note");
            var accountFilter = FindValue(args, "--account");
            bool full = HasFlag(args, "--full");

            var records = LicenseLedger.Load(ledger);
            var filtered = LicenseLedger.Filter(records, accountFilter, noteFilter);
            if (records.Count == 0)
            {
                Console.WriteLine("（暂无发放记录：" + ledger + "）");
                return 0;
            }

            Console.WriteLine("注意保密：台账包含账号明文密码，仅限授权管理员查看。");
            Console.WriteLine("发放台账（" + ledger + "）共 " + records.Count + " 条，匹配 " + filtered.Count + " 条：");
            Console.WriteLine();
            int index = 0;
            foreach (var record in filtered)
            {
                index++;
                Console.WriteLine("{0} | {1} | 账号 {2} | {3} 天 | 签发(UTC) {4} | 密码 {5}",
                    index,
                    string.IsNullOrEmpty(record.Institution) ? "（未填机构）" : record.Institution,
                    record.Account,
                    record.Days,
                    record.IssuedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    record.Password);
                if (full)
                {
                    Console.WriteLine("   卡指纹：" + record.CardFingerprint);
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

            var publicPath = Path.Combine(KeysDir(args), "public_key.pem");
            if (!File.Exists(publicPath))
            {
                Console.WriteLine("未找到公钥文件：" + publicPath);
                Console.WriteLine("请先运行 init 生成密钥对。");
                return 1;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(publicPath));
            var check = LicenseCrypto.VerifyCard(args[1], rsa);
            PrintCardCheck(check, signatureChecked: true);
            return check.IsFullyValid ? 0 : 1;
        }

        private static int Inspect(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("请提供要解析的激活卡号。");
            }

            var check = LicenseCrypto.InspectCard(args[1]);
            PrintCardCheck(check, signatureChecked: false);
            return check.Parsed ? 0 : 1;
        }

        private static void PrintCardCheck(LicenseCardCheck check, bool signatureChecked)
        {
            Console.WriteLine("解析成功：" + (check.Parsed ? "是" : "否"));
            Console.WriteLine("签名校验：" + (signatureChecked ? (check.SignatureValid ? "有效" : "无效") : "未校验（inspect 仅解析内容）"));
            if (check.Payload != null)
            {
                var payload = check.Payload;
                Console.WriteLine("账号：" + payload.Account);
                Console.WriteLine("机构：" + (string.IsNullOrEmpty(payload.Note) ? "（未填）" : payload.Note));
                Console.WriteLine("有效天数：" + payload.ValidityDays + " 天（自首次激活起算）");
                Console.WriteLine("签发时间(本地)：" + FormatLocal(new DateTimeOffset(payload.IssuedAtUtcTicks, TimeSpan.Zero)));
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
        // 文本 / 路径 / 密码辅助
        // ------------------------------------------------------------

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteTextAtomic(string path, string content)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            var text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return text;
            }

            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var source = (name ?? string.Empty).Trim();
            var sb = new StringBuilder(source.Length);
            foreach (var ch in source)
            {
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }

            var result = sb.ToString().Trim();
            while (result.Contains("__", StringComparison.Ordinal))
            {
                result = result.Replace("__", "_", StringComparison.Ordinal);
            }

            if (result.Length == 0)
            {
                return "机构";
            }

            return result.Length > 30 ? result.Substring(0, 30) : result;
        }

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

            for (int i = length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
    }
}