using System;
using System.IO;
using System.Net;
using System.Text;
using TencentTranslate;
using XUnity.AutoTranslator.Plugin.Core.Web;

namespace TencentTranslateTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            const string SecretId = "";
            const string SecretKey = "";

            XUnityWebRequest request = TencentTranslateEndpoint.CreateRequest(
                SecretId,
                SecretKey,
                "en", "zh", "Hello \"World\"! (\")");

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(request.Address);
            req.Method = request.Method;
            req.ContentType = "application/x-www-form-urlencoded"; //v1签名方式所使用的数据类型
            //写入参数数据
            byte[] data = Encoding.UTF8.GetBytes(request.Data);
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }

            //发送并获取响应
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            if (stream == null)
                throw new NullReferenceException();

            //解码响应数据
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                string translation = TencentTranslateEndpoint.ExtractResponse(reader.ReadToEnd());
                Console.WriteLine("获得翻译结果：" + translation);
            }
        }
    }
}
