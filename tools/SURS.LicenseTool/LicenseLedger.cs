using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SURS.LicenseTool
{
    /// <summary>追加式发卡台账记录。密码为本地管理员交付依据，台账必须严格保密。</summary>
    public sealed class LicenseLedgerRecord
    {
        public const string PlaintextWarning = "注意保密：本台账包含账号明文密码，仅限授权管理员保管。";

        [JsonPropertyName("institution")]
        public string Institution { get; set; } = string.Empty;

        [JsonPropertyName("account")]
        public string Account { get; set; } = string.Empty;

        [JsonPropertyName("days")]
        public int Days { get; set; }

        [JsonPropertyName("issuedAt")]
        public DateTimeOffset IssuedAt { get; set; }

        [JsonPropertyName("cardFingerprint")]
        public string CardFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("warning")]
        public string Warning { get; set; } = PlaintextWarning;
    }

    public static class LicenseLedger
    {
        private static readonly Regex DefaultAccountPattern =
            new(@"^hos(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SURS.LicenseTool",
            "ledger.jsonl");

        public static void Append(string ledgerPath, LicenseLedgerRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(ledgerPath)) throw new ArgumentException("台账路径不能为空", nameof(ledgerPath));

            var fullPath = Path.GetFullPath(ledgerPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;
            File.AppendAllText(fullPath, line, new UTF8Encoding(false));
        }

        public static List<LicenseLedgerRecord> Load(string ledgerPath)
        {
            if (string.IsNullOrWhiteSpace(ledgerPath)) throw new ArgumentException("台账路径不能为空", nameof(ledgerPath));
            var fullPath = Path.GetFullPath(ledgerPath);
            var records = new List<LicenseLedgerRecord>();
            if (!File.Exists(fullPath))
            {
                return records;
            }

            int lineNumber = 0;
            foreach (var line in File.ReadLines(fullPath, Encoding.UTF8))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<LicenseLedgerRecord>(line, JsonOptions);
                    if (record == null)
                    {
                        throw new JsonException("记录为空");
                    }
                    records.Add(record);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"台账第 {lineNumber} 行格式无效：{ex.Message}");
                }
            }

            return records;
        }

        public static List<LicenseLedgerRecord> Filter(
            IEnumerable<LicenseLedgerRecord> records,
            string? accountContains,
            string? institutionContains)
        {
            IEnumerable<LicenseLedgerRecord> query = records;
            if (!string.IsNullOrWhiteSpace(accountContains))
            {
                query = query.Where(r => r.Account.Contains(accountContains, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(institutionContains))
            {
                query = query.Where(r => r.Institution.Contains(institutionContains, StringComparison.OrdinalIgnoreCase));
            }
            return query.ToList();
        }

        public static string NextDefaultAccount(IEnumerable<LicenseLedgerRecord> records)
        {
            var list = records.ToList();
            int maxDefault = 0;
            foreach (var record in list)
            {
                var match = DefaultAccountPattern.Match(record.Account ?? string.Empty);
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    maxDefault = Math.Max(maxDefault, index);
                }
            }

            // 续期记录会重复使用原账号，不能用“记录条数+1”分配，否则会跳过序号。
            int next = maxDefault + 1;
            return "hos" + next.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}