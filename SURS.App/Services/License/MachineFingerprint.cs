using System;
using System.Security.Cryptography;
using System.Text;

namespace SURS.App.Services.License
{
    /// <summary>
    /// 机器指纹：用于把本机的授权记录与"这台电脑"绑定，
    /// 从而在“删状态/换卡”途径重置 7 天窗口时进行拦截（防重置）。
    ///
    /// 实现：取操作系统安装时生成的 MachineGuid（HKLM\SOFTWARE\Microsoft\Cryptography）
    /// 做 SHA-256。MachineGuid 在同一系统内稳定、重装系统会变化；
    /// 取不到时用“机器名 + 用户名”兜底（弱但可用的本地标识）。
    ///
    /// 安全边界（如实说明）：
    ///  - 面向普通使用者（临床/信息科）足够——他们不会去读注册表、伪造 GUID；
    ///  - 面向愿意重装系统/克隆 GUID 的恶意逆向者无效（L2），这属于纯离线方案公认的天花板。
    /// </summary>
    public static class MachineFingerprint
    {
        /// <summary>计算本机指纹（64 位十六进制串）。</summary>
        public static string Compute()
        {
            var raw = ReadStableId();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("SURS:v1:" + raw));
            return Convert.ToHexString(bytes);
        }

        private static string ReadStableId()
        {
            try
            {
                var value = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                    "MachineGuid", null);
                if (value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    return "guid:" + s.Trim();
                }
            }
            catch (Exception)
            {
                // 读取失败时走兜底
            }

            return "env:" + Environment.MachineName + "/" + Environment.UserName;
        }
    }
}