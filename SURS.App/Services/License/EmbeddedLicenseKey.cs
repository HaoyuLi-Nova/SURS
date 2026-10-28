using System.Security.Cryptography;

namespace SURS.App.Services.License
{
    /// <summary>
    /// 软件验证端内置的 RSA 公钥（PEM，SPKI 格式）。
    ///
    /// 重要说明：
    /// 1) 这里只放公钥，软件永远不包含私钥；
    /// 2) 当前为演示/示例密钥，仅供联调与交付沟通验收；
    ///    与之配对的私钥由本仓库作者保管在仓库外部，未提交、不发布；
    /// 3) 正式部署时务必用发卡工具重新生成：
    ///       dotnet run --project tools/SURS.LicenseTool -- init
    ///    把输出的公钥粘贴到下方 <see cref="PublicKeyPem"/> 并重新编译，
    ///    与它配对的私钥必须由甲方授权管理员离线妥善保管（严禁入库/发布）。
    /// </summary>
    public static class EmbeddedLicenseKey
    {
        public const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1GlpKlpJIAP0JlmGpi3l
P/vH36fgTFr/eijEkl2N+TCFkEXmeRQ2/0kzMZAAf+tJ0Sp3B9GVkR05/z1yezcq
oQNfVKUCTGyYxno/aidWAeDbcxTZCpSYSniSNZkda8CFMgSxyYkVjddZiz+hXlo4
kMjRwuRIZ+5lTuHe8KFo3e10iQI1FHMIBLwQTKK6UtVWwknTI7lMuLyAiRmGQ1Hz
2uW2+tsX5XJqmcJALKSC4f33nQqftUwVWUogpqGwV5Yak3/IaC1JjM3xb2Ry7MIN
g3/dyvERbO9duBTUhgMi2daWTL+MB+68yxj+J5H0eqnUbzhKvm9FhHjJcik0Xwx9
aQIDAQAB
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