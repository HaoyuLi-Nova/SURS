using System.Security.Cryptography;

namespace SURS.App.Services.License
{
    /// <summary>
    /// 软件验证端内置的 RSA 公钥（PEM，SPKI 格式）。
    ///
    /// 重要说明：
    /// 1) 这里只放公钥，软件永远不包含私钥；
    /// 2) 该公钥为乙方正式发卡密钥，已用于 60 家医院交付与“乙方按周续期”。
    ///    配套私钥由乙方离线保管（%LOCALAPPDATA%\SURS.LicenseTool\.keys 及
    ///    D:\Projects\SURS\乙方机密\ 的加密备份），严禁入库 / 发布 / 随包外发；
    /// 3) 除非甲方授权更换密钥，否则不得改动：
    ///    更换本公钥后，旧 exe 与旧授权卡将全部失效，必须重新签发 60 家并重新发布。
    /// </summary>
    public static class EmbeddedLicenseKey
    {
        public const string PublicKeyPem = @"
    -----BEGIN PUBLIC KEY-----
    MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6DkCma70vqzS8eJ08z2P
    zpHXAPnHZemfeQy+A/5xMd8xky4cgve4d1RUMxOugjZXFn4lIkw2rTcVT/S7iFoZ
    qjmSNPfkUqh61BgL2gvM/ZHagdDW2oppKqtGjB5ITAdMTi7Ts7jWps2LlO+4Q3+L
    VBneleRAuWcp66zSJdVdY0KjMpr/5HCAtdlYZF7miZiTr3y6Sopyb76ahh0ffElh
    f9MkLsS8ZXcdnDfzRJMGpqQVTA92GTeCCqeiMg5rJwZaUIX5fvsFzealKHgoyEhJ
    lZ/eU3tbVTo19w0gGZ5iOjh5A/ev6LrZ7I0Di+GnI0+0MEzDec5Dp7sMD3bWZDHh
    0QIDAQAB
    -----END PUBLIC KEY-----";

        /// <summary>依据内置公钥创建验证器 RSA 实例。</summary>
        public static RSA CreateVerifierRsa()
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            return rsa;
        }
    }
}
