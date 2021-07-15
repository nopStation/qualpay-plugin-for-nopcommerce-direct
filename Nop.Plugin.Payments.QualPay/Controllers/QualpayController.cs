using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Payments.Qualpay.Domain;
using Nop.Plugin.Payments.Qualpay.Models;
using Nop.Plugin.Payments.Qualpay.Services;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Qualpay.Controllers
{
    [Area(AreaNames.Admin)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    public class QualpayController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly QualpayManager _qualpayManager;

        #endregion

        #region Ctor

        public QualpayController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            IStoreContext storeContext,
            ISettingService settingService,
            QualpayManager qualpayManager)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _qualpayManager = qualpayManager;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings =await _settingService.LoadSettingAsync<QualpaySettings>(storeId);

            //prepare model
            var model = new ConfigurationModel
            {
                MerchantId = settings.MerchantId,
                MerchantEmail = settings.MerchantEmail,
                SecurityKey = settings.SecurityKey,
                ProfileId = settings.ProfileId,
                UseSandbox = settings.UseSandbox,
                UseEmbeddedFields = settings.UseEmbeddedFields,
                UseCustomerVault = settings.UseCustomerVault,
                UseRecurringBilling = settings.UseRecurringBilling,
                PaymentTransactionTypeId = (int)settings.PaymentTransactionType,
                AdditionalFee = settings.AdditionalFee,
                AdditionalFeePercentage = settings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeId,
                IsConfigured = !string.IsNullOrEmpty(settings.MerchantId) && long.TryParse(settings.MerchantId, out var merchantId)
            };

            if (storeId > 0)
            {
                model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.UseSandbox, storeId);
                model.UseEmbeddedFields_OverrideForStore =await _settingService.SettingExistsAsync(settings, x => x.UseEmbeddedFields, storeId);
                model.UseCustomerVault_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.UseCustomerVault, storeId);
                model.UseRecurringBilling_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.UseRecurringBilling, storeId);
                model.PaymentTransactionTypeId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.PaymentTransactionType, storeId);
                model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFee, storeId);
                model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFeePercentage, storeId);
            }

            //prepare payment transaction types
            model.PaymentTransactionTypes = (await TransactionType.Authorization.ToSelectListAsync(false))
                .Select(item => new SelectListItem(item.Text, item.Value)).ToList();

            return View("~/Plugins/Payments.Qualpay/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<QualpaySettings>(storeId);

            //ensure that webhook is already exists and create the new one if does not (required for recurring billing)
            if (model.UseRecurringBilling)
            {
                var webhook = await _qualpayManager.CreateWebhook(settings.WebhookId);
                if (webhook?.WebhookId != null)
                {
                    settings.WebhookId = webhook.WebhookId.ToString();
                    settings.WebhookSecretKey = webhook.Secret;
                    await _settingService.SaveSettingAsync(settings, x => x.WebhookId, storeId, false);
                    await _settingService.SaveSettingAsync(settings, x => x.WebhookSecretKey, storeId, false);
                }
                else
                    _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Fields.Webhook.Warning"));
            }

            //save settings
            settings.MerchantId = model.MerchantId;
            settings.SecurityKey = model.SecurityKey;
            settings.ProfileId = model.ProfileId;
            settings.UseSandbox = model.UseSandbox;
            settings.UseEmbeddedFields = model.UseEmbeddedFields;
            settings.UseCustomerVault = model.UseCustomerVault;
            settings.UseRecurringBilling = model.UseRecurringBilling;
            settings.PaymentTransactionType = (TransactionType)model.PaymentTransactionTypeId;
            settings.AdditionalFee = model.AdditionalFee;
            settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            await _settingService.SaveSettingAsync(settings, x => x.MerchantId, storeId, false);
            await _settingService.SaveSettingAsync(settings, x => x.SecurityKey, storeId, false);
            await _settingService.SaveSettingAsync(settings, x => x.ProfileId, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.UseEmbeddedFields, model.UseEmbeddedFields_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.UseCustomerVault, model.UseCustomerVault_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.UseRecurringBilling, model.UseRecurringBilling_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.PaymentTransactionType, model.PaymentTransactionTypeId_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeId, false);

            await _settingService.ClearCacheAsync();

            //display notification
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("subscribe")]
        public async Task<IActionResult> Subscribe(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings
            var settings = await _settingService.LoadSettingAsync<QualpaySettings>();
            if (settings.MerchantEmail == model.MerchantEmail)
                return await Configure();

            //try to subscribe/unsubscribe
            var (success, errorMessage) = await _qualpayManager.SubscribeForQualpayNews(model.MerchantEmail);
            if (success)
            {
                //save settings and display success notification
                settings.MerchantEmail = model.MerchantEmail;
                await _settingService.SaveSettingAsync(settings);

                var message = !string.IsNullOrEmpty(model.MerchantEmail)
                    ? await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Subscribe.Success")
                    : await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Unsubscribe.Success");
                _notificationService.SuccessNotification(message);
            }
            else
            {
                var message = !string.IsNullOrEmpty(errorMessage)
                    ? errorMessage
                    : await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Subscribe.Error");

                _notificationService.ErrorNotification(message);
            }

            return await Configure();
        }

        #endregion
    }
}