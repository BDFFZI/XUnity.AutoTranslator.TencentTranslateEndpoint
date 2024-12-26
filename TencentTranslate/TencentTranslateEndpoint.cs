using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SimpleJSON;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using Random = System.Random;

namespace TencentTranslate
{
    public partial class TencentTranslateEndpoint
    {
        static readonly string[] SupportedLanguages = {
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
        const float QueriesInterval = 0.2f; //腾讯翻译限制最多每秒五次调用，https://cloud.tencent.com/document/product/551/32572

        public static XUnityWebRequest CreateRequest(
            string secretId, string secretKey,
            string source, string target,
            string untranslatedText)
        {
            //随机数，防重放攻击
            Random random = new Random();
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
                { "Source", source }, //翻译的源语言
                { "SourceText", untranslatedText }, //翻译的源文本,增加(@)是为了不让翻译API识别为短语
                { "Target", target }, //翻译的目标语言
                { "Timestamp", dateTimeStamp }, //时间戳：加密用
                { "Version", "2018-03-21" } //API版本：文本翻译固定为2018-03-21
            };
            //添加数字签名
            parameters.Add("Signature", TencentSignatureV1.MakeRequestSignature("POST", "tmt.tencentcloudapi.com", "/", parameters, secretKey));
            return new XUnityWebRequest("POST", "https://tmt.tencentcloudapi.com", TencentSignatureV1.MakeParameterUriString(parameters));
        }
        public static string ExtractResponse(string response)
        {
            // 腾讯文本翻译API返回结果的数据结构：https://cloud.tencent.com/document/product/551/15619
            JSONNode jsonNode = JSON.Parse(response);
            return jsonNode["Response"]["TargetText"].Value;
        }
    }

    public partial class TencentTranslateEndpoint : HttpEndpoint
    {
        public override string Id => "TencentTranslate";
        public override string FriendlyName => "腾讯翻译";

        public override void Initialize(IInitializationContext context)
        {
            secretId = context.GetOrCreateSetting<string>("Tencent", "SecretId", null);
            secretKey = context.GetOrCreateSetting<string>("Tencent", "SecretKey", null);

            if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                throw new Exception("未设置secretId和secretKey");
            if (SupportedLanguages.Contains(context.SourceLanguage) == false)
                throw new Exception("不支持的源语言");
            if (SupportedLanguages.Contains(context.DestinationLanguage) == false)
                throw new Exception("不支持的目标语言");
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
            float timeSinceLast = Time.realtimeSinceStartup - lastRequestTimestamp;
            if (timeSinceLast < QueriesInterval)
                yield return new WaitForSeconds(QueriesInterval - timeSinceLast); //在官方实现中说WaitForSeconds可能无法使用，因为懒暂时不管
            lastRequestTimestamp = Time.realtimeSinceStartup;
        }
        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            //发送翻译请求
            XUnityWebRequest request = CreateRequest(secretId, secretKey, context.SourceLanguage, context.DestinationLanguage, context.UntranslatedText);
            context.Complete(request);
        }
        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            //分析结果
            string translateResult = ExtractResponse(context.Response.Data);
            if (!string.IsNullOrEmpty(translateResult))
                context.Complete(translateResult);
        }

        string secretId;
        string secretKey;
        float lastRequestTimestamp;
    }
}
