using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SURS.App.Services.License;
using SURS.License;
using SURS.LicenseTool;

namespace SURS.LicenseSelfTest
{
    internal static class Program
    {
        private static readonly string MachineA = new('a', 64);
        private static readonly string MachineB = new('b', 64);
        private static readonly List<string> TempRoots = new();
        private static RSA _signingKey = null!;
        private static int _passed;

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            _signingKey = RSA.Create(2048);

            var tests = new (string Name, Action Run)[]
            {
                ("协议-签发验签/全字符篡改/base64url/换行粘贴", ProtocolTests),
                ("生命周期-首次使用/登录/回拨/过期", LifecycleTests),
                ("防重置-有效期内重复激活/同卡复用", AntiReuseTests),
                ("防重置-主状态损坏拦截", CorruptPrimaryStateTests),
                ("防重置-主副本删除后影子恢复", ShadowRestoreTests),
                ("防重置-跨代未到期窗口与旧代残留", LegacyResidueTests),
                ("多机-同一卡可在不同机器独立激活", DifferentMachineTests),
                ("台账-连续签发60家并过滤", LedgerScaleTests),
            };

            try
            {
                foreach (var test in tests)
                {
                    try
                    {
                        test.Run();
                        _passed++;
                        Console.WriteLine("[PASS] " + test.Name);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[FAIL] " + test.Name);
                        Console.WriteLine("       " + ex.Message);
                        return 1;
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"自检全部通过：{_passed}/{tests.Length}");
                return 0;
            }
            finally
            {
                _signingKey.Dispose();
                CleanupTempRoots();
            }
        }

        private static void ProtocolTests()
        {
            var payload = CreatePayload("hos01", "StrongPass!123", 7, "测试医院");
            string card = LicenseCrypto.CreateCard(payload, _signingKey);
            var check = LicenseCrypto.VerifyCard(card, _signingKey);
            Assert(check.IsFullyValid, "合法卡应验签通过");
            AssertEqual("SURS2", card.Split('.')[0], "卡号协议前缀");
            AssertEqual("测试医院", check.Payload!.Note, "机构备注");
            AssertEqual(7, check.Payload.ValidityDays, "授权天数");

            string[] parts = card.Split('.');
            for (int segment = 1; segment <= 2; segment++)
            {
                for (int i = 0; i < parts[segment].Length; i++)
                {
                    char[] chars = parts[segment].ToCharArray();
                    chars[i] = chars[i] == 'A' ? 'B' : 'A';
                    string tampered = segment == 1
                        ? $"SURS2.{new string(chars)}.{parts[2]}"
                        : $"SURS2.{parts[1]}.{new string(chars)}";
                    Assert(!LicenseCrypto.VerifyCard(tampered, _signingKey).IsFullyValid,
                        "篡改卡号第 " + segment + " 段字符 " + i + " 后不得通过");
                }
            }

            Assert(!LicenseCrypto.VerifyCard("SURS1." + parts[1] + "." + parts[2], _signingKey).IsFullyValid,
                "错误协议前缀必须失败");
            Assert(!LicenseCrypto.VerifyCard("not-a-card", _signingKey).IsFullyValid,
                "畸形卡必须失败");

            var wrapped = new StringBuilder();
            for (int i = 0; i < card.Length; i++)
            {
                if (i > 0 && i % 73 == 0)
                {
                    wrapped.Append("\r\n  ");
                }
                wrapped.Append(card[i]);
            }
            Assert(LicenseCrypto.VerifyCard(wrapped.ToString(), _signingKey).IsFullyValid,
                "带空白/换行的卡号粘贴后应还原");

            string canonical = payload.ToCanonicalString();
            Assert(!LicensePayload.TryParseCanonical(
                    canonical.Replace("\naccount=", "\n\naccount="), out _).Success,
                "空行必须拒绝");
            Assert(!LicensePayload.TryParseCanonical(
                    InsertBeforeIssued(canonical, "unknown=x"), out _).Success,
                "未知字段必须拒绝");
            Assert(!LicensePayload.TryParseCanonical(
                    InsertBeforeIssued(canonical, "note=重复"), out _).Success,
                "重复字段必须拒绝");
            Assert(!LicensePayload.TryParseCanonical(
                    canonical.Replace("days=7\n", string.Empty), out _).Success,
                "缺少字段必须拒绝");
            Assert(!LicensePayload.TryParseCanonical(canonical + "\n", out _).Success,
                "尾部空行必须拒绝");

            var reorderedFields = canonical.Split('\n');
            (reorderedFields[0], reorderedFields[1]) = (reorderedFields[1], reorderedFields[0]);
            Assert(!LicensePayload.TryParseCanonical(string.Join("\n", reorderedFields), out _).Success,
                "字段乱序必须拒绝");

            var noNote = CreatePayload("hos02", "AnotherPass!123", 7, string.Empty);
            string noNoteCanonical = noNote.ToCanonicalString();
            Assert(!noNoteCanonical.Contains("note=", StringComparison.Ordinal), "空备注必须省略 note 行");
            AssertEqual(6, noNoteCanonical.Split('\n').Length, "空备注规范串字段数");
            Assert(LicensePayload.TryParseCanonical(noNoteCanonical, out var noNoteParsed).Success,
                "省略备注的规范串应可解析");
            AssertEqual(string.Empty, noNoteParsed!.Note, "省略备注解析结果");

            for (int length = 0; length <= 64; length++)
            {
                byte[] data = RandomNumberGenerator.GetBytes(length);
                string encoded = Base64Url.Encode(data);
                Assert(data.SequenceEqual(Base64Url.Decode(encoded)), "base64url 往返失败，长度=" + length);
            }
        }

        private static void LifecycleTests()
        {
            string root = NewRoot("lifecycle");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string card1 = Issue("hos01", "StrongPass!123", 7, "第一医院");
            string card2 = Issue("hos01", "RenewedPass!456", 7, "第一医院", new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
            var service = NewService(root, MachineA, clock);

            AssertEqual(LicenseStatus.NotActivated, service.StartupGate().Status, "首次启动状态");

            // 先预校验卡内账号密码；错误凭证不得消费卡或开启窗口。
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.ValidateActivationCredentials(card1, "hos01", "wrong-password").Status,
                "首次激活错误密码必须拒绝");
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.ValidateActivationCredentials(card1, "hos02", "StrongPass!123").Status,
                "首次激活错误账号必须拒绝");
            AssertEqual(LicenseStatus.NotActivated, service.StartupGate().Status,
                "错误凭证不得消费卡");

            var activated = service.TryActivate(card1);
            AssertEqual(LicenseStatus.Valid, activated.Status, "首次激活应成功");
            AssertEqual(clock.UtcNow, activated.FirstUseUtc, "首次使用时间");
            AssertEqual(7, activated.RemainingDays, "初始剩余天数");
            Assert(activated.Message.Contains("起 7 天", StringComparison.Ordinal), "应显示 7 天窗口");
            Assert(activated.Message.Contains("剩余 7 天", StringComparison.Ordinal), "应显示剩余天数");

            AssertEqual(LicenseStatus.InvalidCredentials,
                service.CheckLogin("hos01", "wrong-password").Status, "错误密码不得登录");
            AssertEqual(LicenseStatus.Valid, service.StartupGate().Status,
                "错误密码尝试后授权状态仍有效（未被锁死或消耗）");

            AssertEqual(LicenseStatus.Valid, service.CheckLogin("hos01", "StrongPass!123").Status, "正确密码登录");
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.CheckLogin("hos01", "StrongPass!124").Status, "错误密码登录");
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.CheckLogin("hos02", "StrongPass!123").Status, "错误账号登录");
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.CheckLogin("hos01", card1).Status, "激活卡号不得作为登录密码");

            clock.UtcNow = clock.UtcNow.AddDays(1);
            var nextDay = service.StartupGate();
            AssertEqual(LicenseStatus.Valid, nextDay.Status, "一天后仍有效");
            AssertEqual(6, nextDay.RemainingDays, "一天后剩余六天");

            clock.UtcNow = clock.UtcNow.AddMinutes(-15);
            AssertEqual(LicenseStatus.ClockRollback, service.StartupGate().Status, "时钟回拨必须被拦截");

            clock.UtcNow = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
            AssertEqual(LicenseStatus.Expired, service.StartupGate().Status, "168 小时到期");
            AssertEqual(LicenseStatus.Tampered,
                service.TryActivate(card1).Status, "到期后同卡仍不得复用");

            var renewed = service.TryActivate(card2);
            AssertEqual(LicenseStatus.Valid, renewed.Status, "到期后新卡可续期");
            AssertEqual(7, renewed.RemainingDays, "续期窗口重新计算");
            AssertEqual(LicenseStatus.Tampered,
                service.TryActivate(card1).Status, "续期后旧卡消费记录仍保留");
            AssertEqual(LicenseStatus.Valid,
                service.CheckLogin("hos01", "RenewedPass!456").Status, "续期后新密码登录");

            AssertEqual(LicenseStatus.Tampered, service.TryActivate(card2).Status,
                "已消费的授权串不得再次激活");
            AssertEqual(LicenseStatus.InvalidCredentials,
                service.CheckLogin("hos01", card2).Status, "已装卡号不得绕过密码登录");
            Assert(service.InstalledCardEquals(card2), "当前已装卡为续期卡");
            Assert(!service.InstalledCardEquals(card1), "旧卡不再是已装卡");
        }

        private static void AntiReuseTests()
        {
            string root = NewRoot("reuse");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string card1 = Issue("hos01", "StrongPass!123", 7, "复用测试医院");
            string card2 = Issue("hos01", "StrongPass!456", 7, "复用测试医院");
            var service = NewService(root, MachineA, clock);
            AssertEqual(LicenseStatus.Valid, service.TryActivate(card1).Status, "第一张卡激活");
            AssertEqual(LicenseStatus.Tampered, service.TryActivate(card2).Status,
                "有效期内不得换卡重置");
            AssertEqual(LicenseStatus.Tampered, service.TryActivate(card1).Status,
                "同一张卡不得重复激活");
        }

        private static void CorruptPrimaryStateTests()
        {
            string root = NewRoot("corrupt");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string card1 = Issue("hos01", "CorruptPass!123", 7, "损坏测试医院");
            string card2 = Issue("hos01", "CorruptPass!456", 7, "损坏测试医院");
            var service = NewService(root, MachineA, clock);
            AssertEqual(LicenseStatus.Valid, service.TryActivate(card1).Status, "前置激活成功");

            string primaryState = Path.Combine(root, "primary", "license.state");
            File.WriteAllText(primaryState, "非法的状态内容!!", new UTF8Encoding(false));

            AssertEqual(LicenseStatus.Tampered,
                NewService(root, MachineA, clock).StartupGate().Status,
                "主状态损坏必须拦截，不得降级使用");
            AssertEqual(LicenseStatus.Tampered,
                NewService(root, MachineA, clock).CheckLogin("hos01", "CorruptPass!123").Status,
                "主状态损坏时登录必须被拒");
            AssertEqual(LicenseStatus.Tampered,
                NewService(root, MachineA, clock).TryActivate(card2).Status,
                "主状态损坏且窗口未到期时禁止换卡绕过");
        }

        private static void ShadowRestoreTests()
        {
            string root = NewRoot("shadow");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string card1 = Issue("hos01", "StrongPass!123", 7, "影子测试医院");
            string card2 = Issue("hos01", "StrongPass!456", 7, "影子测试医院");

            var firstService = NewService(root, MachineA, clock);
            var activation = firstService.TryActivate(card1);
            AssertEqual(LicenseStatus.Valid, activation.Status, "影子恢复前置激活");
            string primaryState = Path.Combine(root, "primary", "license.state");
            string shadowState = Path.Combine(root, "shadow", "license.state");
            string originalGeneration = ReadField(primaryState, "gen");
            AssertEqual(originalGeneration, ReadField(shadowState, "gen"), "激活后多副本同代");

            File.Delete(primaryState);
            clock.UtcNow = clock.UtcNow.AddHours(2);
            var restored = NewService(root, MachineA, clock).StartupGate();
            AssertEqual(LicenseStatus.Valid, restored.Status, "主状态缺失时影子恢复");
            AssertEqual(activation.FirstUseUtc, restored.FirstUseUtc, "恢复后首次使用时间不变");
            AssertEqual(originalGeneration, ReadField(primaryState, "gen"), "恢复后保持同代");
            Assert(File.Exists(primaryState), "主状态应由影子自动恢复");

            File.Delete(primaryState);
            var crossGeneration = NewService(root, MachineA, clock).TryActivate(card2);
            AssertEqual(LicenseStatus.Tampered, crossGeneration.Status,
                "仅存影子且窗口未到期时必须拒绝换卡");
        }

        private static void LegacyResidueTests()
        {
            string root = NewRoot("legacy");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string oldCard = Issue("hos01", "OldPassword!123", 7, "旧代医院");
            string newCard = Issue("hos01", "NewPassword!456", 7, "新代医院", new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero));
            var service = NewService(root, MachineA, clock);
            AssertEqual(LicenseStatus.Valid, service.TryActivate(oldCard).Status, "旧卡首次激活");

            string primaryState = Path.Combine(root, "primary", "license.state");
            string shadowState = Path.Combine(root, "shadow", "license.state");
            string oldPrimaryState = File.ReadAllText(primaryState, Encoding.UTF8);
            string oldGenerationShadow = File.ReadAllText(shadowState, Encoding.UTF8);
            string oldGeneration = ReadField(shadowState, "gen");

            clock.UtcNow = clock.UtcNow.AddDays(8);
            AssertEqual(LicenseStatus.Valid, service.TryActivate(newCard).Status, "到期后新卡续期");
            string newGeneration = ReadField(primaryState, "gen");
            Assert(oldGeneration != newGeneration, "续期必须生成新状态代");
            AssertEqual(newGeneration, ReadField(shadowState, "gen"), "续期后两文件副本同代");

            // ① 主档被替换回旧代、影子仍是新代 => 跨副本序列冲突，必须拦截。
            File.WriteAllText(primaryState, oldPrimaryState, new UTF8Encoding(false));
            var crossCopy = NewService(root, MachineA, clock).StartupGate();
            AssertEqual(LicenseStatus.Tampered, crossCopy.Status, "主档回放旧代必须拦截");

            // ② 主档与影子都被旧代覆盖，但当前 license.card 已是新卡：
            //    状态中的 activeFp 与卡文件不匹配，必须拦截而不是接受旧代窗口。
            File.Delete(primaryState);
            File.WriteAllText(shadowState, oldGenerationShadow, new UTF8Encoding(false));
            var residue = NewService(root, MachineA, clock).StartupGate();
            AssertEqual(LicenseStatus.Tampered, residue.Status, "只剩旧代残留时不得恢复或重置窗口");
        }

        private static void DifferentMachineTests()
        {
            string rootA = NewRoot("machine-a");
            string rootB = NewRoot("machine-b");
            var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
            string card = Issue("hos10", "SharedCard!123", 7, "多机测试医院");

            var machineA = NewService(rootA, MachineA, clock).TryActivate(card);
            var machineB = NewService(rootB, MachineB, clock).TryActivate(card);
            AssertEqual(LicenseStatus.Valid, machineA.Status, "A 机激活成功");
            AssertEqual(LicenseStatus.Valid, machineB.Status, "B 机可独立激活同一卡");
            AssertEqual(machineA.FirstUseUtc, machineB.FirstUseUtc, "两机可各自从同一时刻起算");

            string copiedRoot = NewRoot("machine-copy");
            Directory.CreateDirectory(Path.Combine(copiedRoot, "primary"));
            Directory.CreateDirectory(Path.Combine(copiedRoot, "shadow"));
            File.Copy(Path.Combine(rootA, "primary", "license.card"), Path.Combine(copiedRoot, "primary", "license.card"));
            File.Copy(Path.Combine(rootA, "primary", "license.state"), Path.Combine(copiedRoot, "primary", "license.state"));
            File.Copy(Path.Combine(rootA, "shadow", "license.state"), Path.Combine(copiedRoot, "shadow", "license.state"));
            AssertEqual(LicenseStatus.Tampered,
                NewService(copiedRoot, MachineB, clock).StartupGate().Status,
                "把 A 机整套状态复制到 B 机必须拦截");
        }

        private static void LedgerScaleTests()
        {
            string root = NewRoot("ledger");
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            var issuedAt = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);

            for (int i = 1; i <= 60; i++)
            {
                var records = LicenseLedger.Load(ledgerPath);
                string account = LicenseLedger.NextDefaultAccount(records);
                string institution = string.Format("测试医院{0:00}", i);
                string password = "HospitalPass!" + i.ToString("00");
                string card = Issue(account, password, 7, institution, issuedAt.AddMinutes(i));
                Assert(LicenseCrypto.VerifyCard(card, _signingKey).IsFullyValid, "批量签发验签：" + account);
                LicenseLedger.Append(ledgerPath, new LicenseLedgerRecord
                {
                    Institution = institution,
                    Account = account,
                    Days = 7,
                    IssuedAt = issuedAt.AddMinutes(i),
                    CardFingerprint = LicenseCrypto.CardFingerprint(card),
                    Password = password,
                });
            }

            var all = LicenseLedger.Load(ledgerPath);
            AssertEqual(60, all.Count, "60 家台账记录数");
            AssertEqual("hos01", all.First().Account, "默认账号从 hos01 开始");
            AssertEqual("hos60", all.Last().Account, "默认账号连续到 hos60");
            AssertEqual("hos61", LicenseLedger.NextDefaultAccount(all), "第 61 家默认账号");
            var projected = all.Concat(new[]
            {
                new LicenseLedgerRecord { Account = "hos01", Institution = "续期记录" }
            }).ToList();
            AssertEqual("hos61", LicenseLedger.NextDefaultAccount(projected), "续期记录不得跳号");
            AssertEqual(1, LicenseLedger.Filter(all, "hos60", null).Count, "账号过滤");
            AssertEqual(1, LicenseLedger.Filter(all, null, "测试医院37").Count, "机构过滤");
            Assert(all.All(r => r.Password.Length >= 12), "台账保存完整密码交付依据");
            Assert(all.All(r => r.Warning.Contains("保密", StringComparison.Ordinal)), "台账带保密警告字段");
        }

        private static LicensePayload CreatePayload(
            string account,
            string password,
            int days,
            string note,
            DateTimeOffset? issuedAt = null)
        {
            byte[] salt = LicenseCrypto.GenerateSalt();
            return new LicensePayload
            {
                Account = account,
                PasswordSaltBase64 = Convert.ToBase64String(salt),
                PasswordHashBase64 = LicenseCrypto.HashPasswordToBase64(password, salt),
                ValidityDays = days,
                Note = note,
                IssuedAtUtcTicks = (issuedAt ?? DateTimeOffset.UtcNow).UtcTicks,
            };
        }

        private static string Issue(
            string account,
            string password,
            int days,
            string note,
            DateTimeOffset? issuedAt = null)
        {
            return LicenseCrypto.CreateCard(CreatePayload(account, password, days, note, issuedAt), _signingKey);
        }

        private static LicenseService NewService(string root, string machineFingerprint, MutableClock clock)
        {
            return new LicenseService(
                logger: null,
                storageDirectory: Path.Combine(root, "primary"),
                nowUtc: clock.Now,
                useRegistryMirror: false,
                shadowDirectory: Path.Combine(root, "shadow"),
                machineFingerprintProvider: () => machineFingerprint,
                verifier: _signingKey);
        }

        private static string InsertBeforeIssued(string canonical, string line)
        {
            int index = canonical.LastIndexOf("\nissued=", StringComparison.Ordinal);
            Assert(index >= 0, "规范串缺少 issued 字段");
            return canonical.Insert(index, "\n" + line);
        }

        private static string ReadField(string path, string field)
        {
            foreach (string line in File.ReadLines(path, Encoding.UTF8))
            {
                if (line.StartsWith(field + "=", StringComparison.Ordinal))
                {
                    return line.Substring(field.Length + 1);
                }
            }
            throw new InvalidOperationException("状态文件缺少字段：" + field);
        }

        private static string NewRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "surs-license-selftest", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            TempRoots.Add(root);
            return root;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message}：期望 {expected}，实际 {actual}");
            }
        }

        private static void CleanupTempRoots()
        {
            foreach (var root in TempRoots)
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch
                {
                    // 自检临时文件清理失败不影响测试结论。
                }
            }
        }

        private sealed class MutableClock
        {
            public MutableClock(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTimeOffset UtcNow { get; set; }

            public DateTimeOffset Now() => UtcNow;
        }
    }
}