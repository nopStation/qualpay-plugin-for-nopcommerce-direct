using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Qualpay.Domain;
using Nop.Plugin.Payments.Qualpay.Models;
using Nop.Plugin.Payments.Qualpay.Services;
using Nop.Plugin.Payments.Qualpay.Validators;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.Qualpay
{
    /// <summary>
    /// Google Analytics plugin
    /// </summary>
    public class QualpayProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly WidgetSettings _widgetSettings;
        private readonly QualpaySettings _qualpaySettings;
        private readonly QualpayManager _qualpayManager;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        #endregion

        #region Ctor

        public QualpayProcessor(ILocalizationService localizationService,
            IWebHelper webHelper,
            ISettingService settingService,
            WidgetSettings widgetSettings,
            QualpaySettings qualpaySettings,
            QualpayManager qualpayManager,
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            _localizationService = localizationService;
            _webHelper = webHelper;
            _settingService = settingService;
            _widgetSettings = widgetSettings;
            _qualpaySettings = qualpaySettings;
            _qualpayManager = qualpayManager;
            _orderTotalCalculationService = orderTotalCalculationService;
        }
        #endregion
        public bool SupportCapture => true;

        public bool SupportPartiallyRefund => true;

        public bool SupportRefund => true;

        public bool SupportVoid => true;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.Automatic;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        public bool SkipPaymentInfo => false;

        public bool HideInWidgetList => true;

        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //try to cancel recurring payment
            var (_, error) = await _qualpayManager.CancelSubscription(cancelPaymentRequest.Order.CustomerId.ToString(),
                cancelPaymentRequest.Order.SubscriptionTransactionId);

            if (!string.IsNullOrEmpty(error))
                return new CancelRecurringPaymentResult { Errors = new[] { error } };

            return new CancelRecurringPaymentResult();
        }
        #region Methods
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            return Task.FromResult(true);
        }

        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            //capture full amount of the authorized transaction
            var (response, error) = await _qualpayManager.CaptureTransaction(capturePaymentRequest.Order.AuthorizationTransactionId,
                Math.Round(capturePaymentRequest.Order.OrderTotal, 2));

            if (!string.IsNullOrEmpty(error))
                return new CapturePaymentResult { Errors = new[] { error } };

            //request succeeded
            return new CapturePaymentResult
            {
                CaptureTransactionId = response.PgId,
                CaptureTransactionResult = response.Rmsg,
                NewPaymentStatus = PaymentStatus.Paid
            };
        }

        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
                _qualpaySettings.AdditionalFee, _qualpaySettings.AdditionalFeePercentage);
        }

        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            var paymentRequest = new ProcessPaymentRequest();

            //pass custom values to payment processor
            var cardId = form[nameof(PaymentInfoModel.BillingCardId)];
            if (!StringValues.IsNullOrEmpty(cardId) && !cardId.FirstOrDefault().Equals(Guid.Empty.ToString()))
                paymentRequest.CustomValues.Add(await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Customer.Card"), cardId.FirstOrDefault());

            var saveCardDetails = form[nameof(PaymentInfoModel.SaveCardDetails)];
            if (!StringValues.IsNullOrEmpty(saveCardDetails) && bool.TryParse(saveCardDetails.FirstOrDefault(), out var saveCard) && saveCard)
                paymentRequest.CustomValues.Add(await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Customer.Card.Save"), true);

            if (_qualpaySettings.UseEmbeddedFields)
            {
                //card details is already validated and tokenized by Qualpay
                var tokenizedCardId = form[nameof(PaymentInfoModel.TokenizedCardId)];
                if (!StringValues.IsNullOrEmpty(tokenizedCardId))
                    paymentRequest.CustomValues.Add(await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Customer.Card.Token"), tokenizedCardId.FirstOrDefault());
            }
            else
            {
                //set card details
                paymentRequest.CreditCardName = form[nameof(PaymentInfoModel.CardholderName)];
                paymentRequest.CreditCardNumber = form[nameof(PaymentInfoModel.CardNumber)];
                paymentRequest.CreditCardExpireMonth = int.Parse(form[nameof(PaymentInfoModel.ExpireMonth)]);
                paymentRequest.CreditCardExpireYear = int.Parse(form[nameof(PaymentInfoModel.ExpireYear)]);
                paymentRequest.CreditCardCvv2 = form[nameof(PaymentInfoModel.CardCode)];
            }

            return paymentRequest;
        }

        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.PaymentMethodDescription");
        }

        public string GetPublicViewComponentName()
        {
            return QualpayDefaults.PAYMENT_INFO_VIEW_COMPONENT_NAME;
        }

        public string GetWidgetViewComponentName(string widgetZone)
        {
            return QualpayDefaults.CUSTOMER_VIEW_COMPONENT_NAME;
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string> { AdminWidgetZones.CustomerDetailsBlock });
        }

        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(false);
        }

        public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            return Task.CompletedTask;
            //throw new System.NotImplementedException();
        }

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            //get response
            var (response, error) =
                _qualpaySettings.PaymentTransactionType == TransactionType.Authorization ? await _qualpayManager.Authorize(processPaymentRequest) :
                _qualpaySettings.PaymentTransactionType == TransactionType.Sale ? await _qualpayManager.Sale(processPaymentRequest) :
                throw new ArgumentException("Transaction type is not supported", nameof(_qualpaySettings.PaymentTransactionType));

            if (!string.IsNullOrEmpty(error))
                return new ProcessPaymentResult { Errors = new[] { error } };

            //request succeeded
            var result = new ProcessPaymentResult
            {
                AvsResult = response.AuthAvsResult,
                Cvv2Result = response.AuthCvv2Result,
                AuthorizationTransactionCode = response.AuthCode
            };

            //set an authorization details
            if (_qualpaySettings.PaymentTransactionType == TransactionType.Authorization)
            {
                result.AuthorizationTransactionId = response.PgId;
                result.AuthorizationTransactionResult = response.Rmsg;
                result.NewPaymentStatus = PaymentStatus.Authorized;
            }

            //or set a capture details
            if (_qualpaySettings.PaymentTransactionType == TransactionType.Sale)
            {
                result.CaptureTransactionId = response.PgId;
                result.CaptureTransactionResult = response.Rmsg;
                result.NewPaymentStatus = PaymentStatus.Paid;
            }
            return result;
        }

        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            //create subscription for recurring billing
            var (subscription, error) = await _qualpayManager.CreateSubscription(processPaymentRequest);

            if (!string.IsNullOrEmpty(error))
                return new ProcessPaymentResult { Errors = new[] { error } };

            //request succeeded
            return new ProcessPaymentResult
            {
                SubscriptionTransactionId = subscription.SubscriptionId.ToString(),
                AuthorizationTransactionCode = subscription.Response?.AuthCode,
                AuthorizationTransactionId = subscription.Response?.PgId,
                CaptureTransactionId = subscription.Response?.PgId,
                CaptureTransactionResult = subscription.Response?.Rmsg,
                AuthorizationTransactionResult = subscription.Response?.Rmsg,
                AvsResult = subscription.Response?.AvsResult,
                Cvv2Result = subscription.Response?.Cvv2Result,
                NewPaymentStatus = PaymentStatus.Paid
            };
        }

        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            //refund full or partial amount of the captured transaction
            var (_, error) = await _qualpayManager.Refund(refundPaymentRequest.Order.CaptureTransactionId,
                Math.Round(refundPaymentRequest.AmountToRefund, 2));

            if (!string.IsNullOrEmpty(error))
                return new RefundPaymentResult { Errors = new[] { error } };

            //request succeeded
            return new RefundPaymentResult
            {
                NewPaymentStatus = refundPaymentRequest.IsPartialRefund
                    ? PaymentStatus.PartiallyRefunded
                    : PaymentStatus.Refunded
            };
        }

        public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            if (_qualpaySettings.UseEmbeddedFields)
            {
                //try to get errors from Qualpay card tokenization
                if (form.TryGetValue(nameof(PaymentInfoModel.Errors), out var errorsString) && !StringValues.IsNullOrEmpty(errorsString))
                    return errorsString.ToString().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            else
            {
                //validate payment info (custom validation)
                var validationResult = new PaymentInfoValidator(_localizationService).Validate(new PaymentInfoModel
                {
                    CardholderName = form[nameof(PaymentInfoModel.CardholderName)],
                    CardNumber = form[nameof(PaymentInfoModel.CardNumber)],
                    ExpireMonth = form[nameof(PaymentInfoModel.ExpireMonth)],
                    ExpireYear = form[nameof(PaymentInfoModel.ExpireYear)],
                    CardCode = form[nameof(PaymentInfoModel.CardCode)],
                    BillingCardId = form[nameof(PaymentInfoModel.BillingCardId)],
                    SaveCardDetails = form.TryGetValue(nameof(PaymentInfoModel.SaveCardDetails), out var saveCardDetails)
                        && bool.TryParse(saveCardDetails.FirstOrDefault(), out var saveCard)
                        && saveCard
                });
                if (!validationResult.IsValid)
                    return validationResult.Errors.Select(error => error.ErrorMessage).ToList();
            }

            return new List<string>();
        }

        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            //void full amount of the authorized transaction
            var (_, error) = await _qualpayManager.VoidTransaction(voidPaymentRequest.Order.AuthorizationTransactionId);

            if (!string.IsNullOrEmpty(error))
                return new VoidPaymentResult { Errors = new[] { error } };

            //request succeeded
            return new VoidPaymentResult { NewPaymentStatus = PaymentStatus.Voided };
        }


        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new QualpaySettings
            {
                UseSandbox = true,
                UseEmbeddedFields = true,
                UseCustomerVault = true,
                PaymentTransactionType = TransactionType.Sale
            });

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(QualpayDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(QualpayDefaults.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Enums.Nop.Plugin.Payments.Qualpay.Domain.Authorization"] = "Authorization",
                ["Enums.Nop.Plugin.Payments.Qualpay.Domain.Sale"] = "Sale (authorization and capture)",
                ["Plugins.Payments.Qualpay.Customer"] = "Qualpay Vault Customer",
                ["Plugins.Payments.Qualpay.Customer.Card"] = "Use a previously saved card",
                ["Plugins.Payments.Qualpay.Customer.Card.ExpirationDate"] = "Expiration date",
                ["Plugins.Payments.Qualpay.Customer.Card.Id"] = "ID",
                ["Plugins.Payments.Qualpay.Customer.Card.MaskedNumber"] = "Card number",
                ["Plugins.Payments.Qualpay.Customer.Card.Save"] = "Save card data for future purchases",
                ["Plugins.Payments.Qualpay.Customer.Card.Select"] = "Select a card",
                ["Plugins.Payments.Qualpay.Customer.Card.Token"] = "Use a tokenized card",
                ["Plugins.Payments.Qualpay.Customer.Card.Type"] = "Type",
                ["Plugins.Payments.Qualpay.Customer.Create"] = "Add to Vault",
                ["Plugins.Payments.Qualpay.Customer.Hint"] = "Qualpay Vault Customer ID",
                ["Plugins.Payments.Qualpay.Customer.NotExists"] = "The customer is not yet in Qualpay Customer Vault",
                ["Plugins.Payments.Qualpay.Fields.AdditionalFee"] = "Additional fee",
                ["Plugins.Payments.Qualpay.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
                ["Plugins.Payments.Qualpay.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
                ["Plugins.Payments.Qualpay.Fields.AdditionalFeePercentage.Hint"] = "Determine whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
                ["Plugins.Payments.Qualpay.Fields.MerchantEmail"] = "Email",
                ["Plugins.Payments.Qualpay.Fields.MerchantEmail.Hint"] = "Enter your email to subscribe to Qualpay news.",
                ["Plugins.Payments.Qualpay.Fields.MerchantId"] = "Merchant ID",
                ["Plugins.Payments.Qualpay.Fields.MerchantId.Hint"] = "Specify your Qualpay merchant identifier.",
                ["Plugins.Payments.Qualpay.Fields.MerchantId.Required"] = "Merchant ID is required if a Security key is present.",
                ["Plugins.Payments.Qualpay.Fields.PaymentTransactionType"] = "Transaction type",
                ["Plugins.Payments.Qualpay.Fields.PaymentTransactionType.Hint"] = "Choose payment transaction type.",
                ["Plugins.Payments.Qualpay.Fields.ProfileId"] = "Profile ID",
                ["Plugins.Payments.Qualpay.Fields.ProfileId.Hint"] = "Specify your Qualpay profile identifier.",
                ["Plugins.Payments.Qualpay.Fields.ProfileId.Required"] = "Profile ID is required when Qualpay Recurring Billing is enabled.",
                ["Plugins.Payments.Qualpay.Fields.SecurityKey"] = "Security key",
                ["Plugins.Payments.Qualpay.Fields.SecurityKey.Hint"] = "Specify your Qualpay security key.",
                ["Plugins.Payments.Qualpay.Fields.SecurityKey.Required"] = "Security key is required if a Merchant ID is present.",
                ["Plugins.Payments.Qualpay.Fields.UseCustomerVault"] = "Use Customer Vault",
                ["Plugins.Payments.Qualpay.Fields.UseCustomerVault.Hint"] = "Determine whether to use Qualpay Customer Vault feature. The Customer Vault reduces the amount of associated payment data that touches your servers and enables subsequent payment billing information to be fulfilled by Qualpay.",
                ["Plugins.Payments.Qualpay.Fields.UseEmbeddedFields"] = "Use Embedded Fields",
                ["Plugins.Payments.Qualpay.Fields.UseEmbeddedFields.Hint"] = "Determine whether to use Qualpay Embedded Fields feature. Your customer will remain on your website, but payment information is collected and processed on Qualpay servers. Since your server is not processing customer payment data, your PCI DSS compliance scope is greatly reduced.",
                ["Plugins.Payments.Qualpay.Fields.UseEmbeddedFields.TransientKey.Required"] = "Qualpay Embedded Fields cannot be invoked without a transient key",
                ["Plugins.Payments.Qualpay.Fields.UseRecurringBilling"] = "Use Recurring Billing",
                ["Plugins.Payments.Qualpay.Fields.UseRecurringBilling.Hint"] = "Determine whether to use Qualpay Recurring Billing feature. Support setting your customers up for recurring or subscription payments.",
                ["Plugins.Payments.Qualpay.Fields.UseSandbox"] = "Use Sandbox",
                ["Plugins.Payments.Qualpay.Fields.UseSandbox.Hint"] = "Determine whether to enable sandbox (testing environment).",
                ["Plugins.Payments.Qualpay.Fields.Webhook.Warning"] = "Webhook was not created (you'll not be able to handle recurring payments)",
                ["Plugins.Payments.Qualpay.PaymentMethodDescription"] = "Pay by credit / debit card using Qualpay payment gateway",
                ["Plugins.Payments.Qualpay.Subscribe"] = "Stay informed",
                ["Plugins.Payments.Qualpay.Subscribe.Error"] = "An error has occurred",
                ["Plugins.Payments.Qualpay.Subscribe.Success"] = "You have subscribed to Qualpay news",
                ["Plugins.Payments.Qualpay.Unsubscribe.Success"] = "You have unsubscribed from Qualpay news"
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //settings
            if (_widgetSettings.ActiveWidgetSystemNames.Contains(QualpayDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(QualpayDefaults.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }
            await _settingService.DeleteSettingAsync<QualpaySettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Enums.Nop.Plugin.Payments.Qualpay");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Qualpay");

            await base.UninstallAsync();
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/Qualpay/Configure";
        }
        #endregion

    }
}