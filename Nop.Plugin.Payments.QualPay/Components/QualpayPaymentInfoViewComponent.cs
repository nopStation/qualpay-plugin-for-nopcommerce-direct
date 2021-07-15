using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Qualpay.Models;
using Nop.Plugin.Payments.Qualpay.Services;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Qualpay.Components
{
    /// <summary>
    /// Represents payment info view component
    /// </summary>
    [ViewComponent(Name = QualpayDefaults.PAYMENT_INFO_VIEW_COMPONENT_NAME)]
    public class QualpayPaymentInfoViewComponent : NopViewComponent
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly QualpayManager _qualpayManager;
        private readonly QualpaySettings _qualpaySettings;

        #endregion

        #region Ctor

        public QualpayPaymentInfoViewComponent(ICustomerService customerService,
            ILocalizationService localizationService,
            ILogger logger,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWorkContext workContext,
            QualpayManager qualpayManager,
            QualpaySettings qualpaySettings)
        {
            _customerService = customerService;
            _localizationService = localizationService;
            _logger = logger;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _workContext = workContext;
            _qualpayManager = qualpayManager;
            _qualpaySettings = qualpaySettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>View component result</returns>
        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            //prepare payment model
            var model = new PaymentInfoModel();

            //prepare years
            for (var i = 0; i < 15; i++)
            {
                var year = (DateTime.Now.Year + i).ToString();
                model.ExpireYears.Add(new SelectListItem { Text = year, Value = year, });
            }

            //prepare months
            for (var i = 1; i <= 12; i++)
            {
                model.ExpireMonths.Add(new SelectListItem { Text = i.ToString("D2"), Value = i.ToString(), });
            }

            //try to get transient key for Qualpay Embedded Fields
            if (_qualpaySettings.UseEmbeddedFields)
            {
                var key = (await _qualpayManager.GetTransientKey())?.TransientKey;
                if (string.IsNullOrEmpty(key))
                {
                    //Qualpay Embedded Fields cannot be invoked without a transient key
                    _qualpaySettings.UseEmbeddedFields = false;
                    await _settingService.SaveSettingAsync(_qualpaySettings);
                    await _logger.WarningAsync(await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Fields.UseEmbeddedFields.TransientKey.Required"));
                }
                else
                    model.TransientKey = key;
            }

            //prepare Qualpay Customer Vault model for non-guest customer
            model.IsGuest = await _customerService.IsGuestAsync(await _workContext.GetCurrentCustomerAsync());
            if (_qualpaySettings.UseCustomerVault && !model.IsGuest)
            {
                //try to get customer billing cards
                model.BillingCards = (await _qualpayManager.GetCustomerCards((await _workContext.GetCurrentCustomerAsync()).Id))
                    .Select(card => new SelectListItem { Text = card.CardNumber, Value = card.CardId }).ToList();

                if (model.BillingCards.Any())
                {
                    //select the first actual card by default
                    model.BillingCardId = model.BillingCards.FirstOrDefault().Value;

                    //add the special item for 'select card' with empty GUID value 
                    var selectCardText = await _localizationService.GetResourceAsync("Plugins.Payments.Qualpay.Customer.Card.Select");
                    model.BillingCards.Insert(0, new SelectListItem { Text = selectCardText, Value = Guid.Empty.ToString() });
                }

                //whether it's a recurring order
                var currentShoppingCart = (await _shoppingCartService
                    .GetShoppingCartAsync(await _workContext.GetCurrentCustomerAsync(), ShoppingCartType.ShoppingCart, (await _storeContext.GetCurrentStoreAsync()).Id)).ToList();
                model.IsRecurringOrder = await _shoppingCartService.ShoppingCartIsRecurringAsync(currentShoppingCart);
            }

            return View("~/Plugins/Payments.Qualpay/Views/PaymentInfo.cshtml", model);
        }

        #endregion
    }
}