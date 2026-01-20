using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq; // 对应 XML 解析
using System.Linq;

namespace CasClientDemo
{
    public class CasUtil
    {
        // HttpClient 建议静态复用
        private static readonly HttpClient client = new HttpClient();

        public static async Task Main(string[] args)
        {
            string casUrl = "https://authserver.ynftc.cn/authserver";
            string service = "http://casp.ynftc.cn/qljfwapp/";
            string ticket = "ST-1023038-XQhlr5FH-rxLjlimfJ3dfreHr5Elocalhost"; 

            try
            {
                CasUser? casUser = await GetCasUserAsync(casUrl, service, ticket);
                
                if (casUser != null)
                {
                    Console.WriteLine("获取登录用户姓名：" + casUser.Name);
                    Console.WriteLine("获取登录用户ID：" + casUser.CampusNo);
                }
                else
                {
                    Console.WriteLine("未获取到用户，可能是 Ticket 无效。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生错误: " + ex.Message);
            }
        }

        public static async Task<CasUser?> GetCasUserAsync(string casUrl, string service, string ticket)
        {
            // 发送请求
            string casRtnStr = await SendGetRequestAsync(casUrl, service, ticket);
            Console.WriteLine(casRtnStr);

            // 解析 XML
            CasUser? casUser = ParseCasUser(casRtnStr);

            if (casUser != null)
            {
                Console.WriteLine("获取登录用户：" + casUser.CampusNo);
            }
            else
            {
                Console.Error.WriteLine("ticket换用户信息失败：" + casRtnStr);
            }

            return casUser;
        }

        private static async Task<string> SendGetRequestAsync(string casUrl, string service, string ticket)
        {
            // C# 使用 Uri.EscapeDataString 进行 URL 编码，对应 Java 的 URLEncoder.encode
            string url = $"{casUrl}/serviceValidate?service={Uri.EscapeDataString(service)}&ticket={ticket}";

            // 使用 HttpClient 发送 GET 请求
            return await client.GetStringAsync(url);
        }

        private static CasUser? ParseCasUser(string xmlContent)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlContent);

                // CAS 返回的 XML 通常带有 Namespace (例如 cas:serviceResponse)
                // 我们不需要关心具体的 namespace URL，直接查找 LocalName 包含 authenticationSuccess 的节点
                var successNode = doc.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "authenticationSuccess");

                if (successNode == null)
                {
                    return null; // 验证失败
                }

                CasUser user = new CasUser();

                // 遍历 authenticationSuccess 下的所有子节点（包括深层嵌套的 attributes）
                foreach (var element in successNode.Descendants())
                {
                    // 对应 Java 代码中的 qName.replaceFirst("cas:", "")
                    // C# XElement.Name.LocalName 自动忽略前缀
                    string localName = element.Name.LocalName;
                    string content = element.Value.Trim();

                    if (string.IsNullOrEmpty(content)) continue;

                    switch (localName)
                    {
                        case "cn":
                            user.Name = content;
                            break;
                        case "eduPersonOrgDN":
                            user.DepNo = content;
                            break;
                        case "sn":
                            user.Surname = content;
                            break;
                        case "uid":
                            user.CampusNo = content;
                            break;
                        case "eduPersonSex":
                            user.Gender = content;
                            break;
                        case "inetUserStatus":
                            user.UserStatus = content;
                            break;
                        case "iplanet-am-user-alias-list":
                            user.UserAlias = content;
                            break;
                        case "mail":
                            user.UserMail = content;
                            break;
                        case "memberOf":
                            user.Memberof = content;
                            break;
                            // 可以继续添加其他字段
                    }
                }

                return user;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("XML解析异常: " + ex.Message);
                return null;
            }
        }
    }

    // 实体类
    public class CasUser
    {
        public string? Surname { get; set; }
        public string? Name { get; set; }
        public string? DepNo { get; set; }
        public string? DepName { get; set; }
        public string? Gender { get; set; }
        public string? UserStatus { get; set; }
        public string? UserAlias { get; set; }
        public string? UserMail { get; set; }
        public string? TelPhone { get; set; }
        public string? CampusNo { get; set; }
        public string? Memberof { get; set; }
    }
}
