using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using Random = System.Random;


public partial class TencentTranslate
{
    static readonly string[] supportedLanguages = {
        "zh", //简体中文
        "zh-HK", //繁体中文
        "zh-TW", // 繁体中文
        "zh-TR", // 繁体中文
        "en", //英语
        "ar", //阿拉伯语
        "de", //德语
        "es", //西班牙语
        "fr", //法语
        "it", //意大利语
        "ja", //日语
        "pt", //葡萄牙语
        "ru", //俄语
        "ko", //韩语
        "km", //高棉语
        "lo", //老挝语
    };
    const float queriesInterval = 0.2f; //腾讯翻译限制最多每秒五次调用，https://cloud.tencent.com/document/product/551/32572
    static readonly Random random = new Random();
    static readonly Regex regex = new Regex("\"TargetText\":\"(.*)\""); // 腾讯文本翻译API返回结果的数据结构：https://cloud.tencent.com/document/product/551/15619

    // 旧版网络通信实现
    // /// <summary>
    // /// 使用Post方法发送Http请求报文，使用该方法你可能还需要考虑网络凭证的问题
    // /// HttpPost("https://tmt.tencentcloudapi.com", TencentSignatureV1.MakeParameterUriString(parameters));
    // /// </summary>
    // /// <param name="requestUri"></param>
    // /// <param name="requestData"></param>
    // /// <returns></returns>
    // static string HttpPost(string requestUri, string requestData)
    // {
    //     HttpWebRequest req = (HttpWebRequest)WebRequest.Create(requestUri);
    //
    //     req.Method = "POST";
    //     req.ContentType = "application/x-www-form-urlencoded"; //v1签名方式所使用的数据类型
    //     //写入参数数据
    //     byte[] data = Encoding.UTF8.GetBytes(requestData);
    //     req.ContentLength = data.Length;
    //     using (Stream reqStream = req.GetRequestStream())
    //     {
    //         reqStream.Write(data, 0, data.Length);
    //         reqStream.Close();
    //     }
    //
    //     //发送并获取响应
    //     HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
    //     Stream stream = resp.GetResponseStream();
    //     if (stream == null)
    //         return null;
    //
    //     //解码响应数据
    //     using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
    //     {
    //         return reader.ReadToEnd();
    //     }
    // }
    // /// <summary>
    // /// 设置网络凭证
    // /// ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
    // /// </summary>
    // /// <param name="sender"></param>
    // /// <param name="certificate"></param>
    // /// <param name="chain"></param>
    // /// <param name="sslPolicyErrors"></param>
    // /// <returns></returns>
    // bool MyRemoteCertificateValidationCallback(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    // {
    //     bool isOk = true;
    //     // If there are errors in the certificate chain,
    //     // look at each error to determine the cause.
    //     if (sslPolicyErrors != SslPolicyErrors.None)
    //     {
    //         for (int i = 0; i < chain.ChainStatus.Length; i++)
    //         {
    //             if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown)
    //             {
    //                 continue;
    //             }
    //             chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
    //             chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
    //             chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
    //             chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
    //             bool chainIsValid = chain.Build((X509Certificate2)certificate);
    //             if (!chainIsValid)
    //             {
    //                 isOk = false;
    //                 break;
    //             }
    //         }
    //     }
    //     return isOk;
    // }
}

public partial class TencentTranslate : HttpEndpoint, ITranslateEndpoint
{
    public override string Id => "TencentTranslate";
    public override string FriendlyName => "腾讯翻译";

    public override void Initialize(IInitializationContext context)
    {
        secretId = context.GetOrCreateSetting<string>("Tencent", "SecretId", null);
        secretKey = context.GetOrCreateSetting<string>("Tencent", "SecretKey", null);

        if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            throw new Exception("未设置secretId和secretKey");
        if (supportedLanguages.Contains(context.SourceLanguage) == false)
            throw new Exception("不支持的源语言");
        if (supportedLanguages.Contains(context.DestinationLanguage) == false)
            throw new Exception("不支持的目标语言");
    }

    public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
    {
        float timeSinceLast = Time.realtimeSinceStartup - lastRequestTimestamp;
        if (timeSinceLast < queriesInterval)
            yield return new WaitForSeconds(queriesInterval - timeSinceLast); //在官方实现中说WaitForSeconds可能无法使用，因为懒暂时不管
        lastRequestTimestamp = Time.realtimeSinceStartup;
    }
    public override void OnCreateRequest(IHttpRequestCreationContext context)
    {
        //时间和时间戳信息
        DateTime dateTime = DateTime.Now;
        string dateTimeStamp = ((dateTime.ToUniversalTime().Ticks - 621355968000000000) / 10000000).ToString();

        //准备参数信息，参数顺序不要变，腾讯v1签名有参数排序要求
        Dictionary<string, string> parameters = new Dictionary<string, string>(11) {
            { "Action", "TextTranslate" }, //调用的API：文本翻译
            { "Nonce", random.Next(0, int.MaxValue).ToString() }, //随机数，防重放攻击
            { "ProjectId", "0" }, //用于项目管理功能，不使用则填0
            { "Region", "ap-shanghai" }, //处理调用服务器：上海
            { "SecretId", secretId }, //申请的API账号
            { "Source", context.SourceLanguage }, //翻译的源语言
            { "SourceText", context.UntranslatedText }, //翻译的源文本
            { "Target", context.DestinationLanguage }, //翻译的目标语言
            { "Timestamp", dateTimeStamp }, //时间戳：加密用
            { "Version", "2018-03-21" } //API版本：文本翻译固定为2018-03-21
        };
        //添加数字签名
        parameters.Add("Signature", TencentSignatureV1.MakeRequestSignature("POST", "tmt.tencentcloudapi.com", "/", parameters, secretKey));

        //发送翻译请求
        context.Complete(new XUnityWebRequest("POST", "https://tmt.tencentcloudapi.com", TencentSignatureV1.MakeParameterUriString(parameters)));
    }
    public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
    {
        //分析结果
        string translateResult = regex.Match(context.Response.Data).Groups[1].ToString();
        if (string.IsNullOrEmpty(translateResult) != true)
            context.Complete(translateResult);
    }

    string secretId;
    string secretKey;
    float lastRequestTimestamp;
}
