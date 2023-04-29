// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// 用于实现腾讯v1版本签名的函数
/// https://cloud.tencent.com/document/api/551/15616
public static class TencentSignatureV1
{
    /// <summary>
    /// 生成针对请求信息的数字签名
    /// </summary>
    /// <param name="requestMethod">HTTP的请求方法：本项目基于“POST”方法</param>
    /// <param name="requestHost">HTTP的通信服务器：文本翻译的服务器为“tmt.tencentcloudapi.com”</param>
    /// <param name="requestPath">API请求路径：文本翻译的API请求路径为“/”</param>
    /// <param name="requestParams">API请求参数</param>
    /// <param name="signKey">用于签名的密钥</param>
    /// <returns></returns>
    public static string MakeRequestSignature(string requestMethod, string requestHost, string requestPath, Dictionary<string, string> requestParams, string signKey)
    {
        //拼接用于签名的字符串原文
        StringBuilder message = new StringBuilder();
        message.Append(requestMethod);
        message.Append(requestHost);
        message.Append(requestPath);
        message.Append("?");
        foreach (KeyValuePair<string, string> param in requestParams)
        {
            message.Append(param.Key);
            message.Append("=");
            message.Append(param.Value);
            message.Append("&");
        }
        message.Length--;

        //使用HMAC-SHA1算法对原文进行签名
        using (HMACSHA1 sha1 = new HMACSHA1(Encoding.UTF8.GetBytes(signKey)))
        {
            byte[] signature = sha1.ComputeHash(Encoding.UTF8.GetBytes(message.ToString()));
            return Convert.ToBase64String(signature);
        }
    }
    /// <summary>
    /// 生成用于Post的数据
    /// </summary>
    /// <param name="requestParams"></param>
    /// <returns></returns>
    public static string MakeParameterUriString(Dictionary<string, string> requestParams)
    {
        StringBuilder result = new StringBuilder();
        foreach (KeyValuePair<string, string> param in requestParams)
        {
            result.Append(param.Key);
            result.Append("=");
            result.Append(Uri.EscapeDataString(param.Value));
            result.Append("&");
        }
        result.Length--;
        return result.ToString();
    }
}
