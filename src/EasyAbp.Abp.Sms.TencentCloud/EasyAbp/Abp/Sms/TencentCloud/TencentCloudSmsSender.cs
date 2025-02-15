﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyAbp.Abp.Sms.TencentCloud.Settings;
using EasyAbp.Abp.TencentCloud.Common;
using EasyAbp.Abp.TencentCloud.Common.Requester;
using EasyAbp.Abp.TencentCloud.Sms.SendSms;
using Volo.Abp.Json;
using Volo.Abp.Settings;
using Volo.Abp.Sms;

namespace EasyAbp.Abp.Sms.TencentCloud
{
    public class TencentCloudSmsSender : ISmsSender
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ISettingProvider _settingProvider;
        private readonly ITencentCloudApiRequester _requester;

        public TencentCloudSmsSender(
            IJsonSerializer jsonSerializer,
            ISettingProvider settingProvider,
            ITencentCloudApiRequester requester)
        {
            _jsonSerializer = jsonSerializer;
            _settingProvider = settingProvider;
            _requester = requester;
        }

        public virtual async Task SendAsync(SmsMessage smsMessage)
        {
            var request = new SendSmsRequest(
                new[] { smsMessage.PhoneNumber },
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.TemplateIdPropertyName),
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.SmsSdkAppidPropertyName,
                    await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultSmsSdkAppid)),
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.SignPropertyName,
                    await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultSign)),
                GetTemplateParamSet(smsMessage),
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.ExtendCodePropertyName,
                    await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultExtendCode)),
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.SessionContextPropertyName),
                GetStringProperty(smsMessage, AbpSmsTencentCloudConsts.SenderIdPropertyName,
                    await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultSenderId))
            );

            var commonOptions = new AbpTencentCloudCommonOptions
            {
                SecretId = await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultSecretId),
                SecretKey = await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.DefaultSecretKey)
            };

            var response = await _requester.SendRequestAsync<SendSmsResponse>(request,
                await _settingProvider.GetOrNullAsync(AbpSmsTencentCloudSettings.EndPoint), commonOptions);
            if (response.Error != null)
            {
                throw new SmsSendingException(response.Error.Code, response.Error.Message);
            }
            if (!response.SendStatusSet.IsNullOrEmpty())
            {
                var sendStatus = response.SendStatusSet.First();
                if (sendStatus.Code != "Ok")
                {
                    throw new SmsSendingException(sendStatus.Code, sendStatus.Message);
                }
            }
        }

        protected virtual string GetStringProperty(SmsMessage smsMessage, string key, string defaultValue = null)
        {
            var str = smsMessage.Properties.GetOrDefault(key) as string;

            return !str.IsNullOrEmpty() ? str : defaultValue;
        }

        protected virtual string[] GetTemplateParamSet(SmsMessage smsMessage)
        {
            var obj = smsMessage.Properties.GetOrDefault(AbpSmsTencentCloudConsts.TemplateParamSetPropertyName);

            return obj switch
            {
                null => null,
                string str => _jsonSerializer.Deserialize<string[]>(str),
                IEnumerable<string> set => set.ToArray(),
                _ => throw new InvalidTemplateParamSetException()
            };
        }
    }
}