using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Payments.Qualpay.Services
{
    /// <summary>
    /// Represents plugin event consumer
    /// </summary>
    public class EventConsumer :
        IConsumer<EntityInsertedEvent<RecurringPayment>>,
        IConsumer<PageRenderingEvent>
    {
        #region Fields

        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly INopHtmlHelper _nopHtmlHelper;
        private readonly IActionContextAccessor _actionContextAccessor;

        #endregion

        #region Ctor

        public EventConsumer(IOrderService orderService,
            IPaymentPluginManager paymentPluginManager, 
            INopHtmlHelper nopHtmlHelper,
            IActionContextAccessor actionContextAccessor)
        {
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _nopHtmlHelper = nopHtmlHelper;
            _actionContextAccessor = actionContextAccessor;
        }

        #endregion

        #region Methods

        public async Task HandleEventAsync(PageRenderingEvent eventMessage)
        {
            if (_actionContextAccessor.ActionContext.ActionDescriptor == null)
                return;

            //check whether the plugin is active
            if (!await _paymentPluginManager.IsPluginActiveAsync(QualpayDefaults.SystemName))
                return;

            //add Embedded Fields sсript and styles to the one page checkout
            if (eventMessage.GetRouteName()?.Equals(QualpayDefaults.OnePageCheckoutRouteName) ?? false)
            {
                _nopHtmlHelper.AddScriptParts(ResourceLocation.Footer, QualpayDefaults.EmbeddedFieldsScriptPath);
                _nopHtmlHelper.AddCssFileParts(QualpayDefaults.EmbeddedFieldsStylePath, string.Empty);
            }
        }

        public async Task HandleEventAsync(EntityInsertedEvent<RecurringPayment> eventMessage)
        {
            var recurringPayment = eventMessage?.Entity;
            if (recurringPayment == null)
                return;

            //add first payment to history right after creating recurring payment
            await _orderService.InsertRecurringPaymentHistoryAsync(new RecurringPaymentHistory
            {
                RecurringPaymentId = recurringPayment.Id,
                CreatedOnUtc = DateTime.UtcNow,
                OrderId = recurringPayment.InitialOrderId,
            });
        }

        #endregion
    }
}