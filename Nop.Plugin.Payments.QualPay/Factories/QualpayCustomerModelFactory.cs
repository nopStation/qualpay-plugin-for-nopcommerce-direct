using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Payments.Qualpay.Models.Customer;
using Nop.Plugin.Payments.Qualpay.Services;
using Nop.Services.Common;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.Payments.Qualpay.Factories
{
    /// <summary>
    /// Represents Qualpay customer model factory
    /// </summary>
    public class QualpayCustomerModelFactory
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWorkContext _workContext;
        private readonly QualpayManager _qualpayManager;

        #endregion

        #region Ctor

        public QualpayCustomerModelFactory(IGenericAttributeService genericAttributeService,
            IWorkContext workContext,
            QualpayManager qualpayManager)
        {
            _genericAttributeService = genericAttributeService;
            _workContext = workContext;
            _qualpayManager = qualpayManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Prepare Qualpay customer model
        /// </summary>
        /// <param name="customerModel">Customer model</param>
        /// <param name="customer">Customer</param>
        /// <returns>Qualpay customer model</returns>
        public async Task<QualpayCustomerModel> PrepareQualpayCustomerModel(CustomerModel customerModel, Customer customer)
        {
            if (customerModel == null)
                throw new ArgumentNullException(nameof(customerModel));

            if (customer == null)
                throw new ArgumentNullException(nameof(customer));

            //try to get a customer from the Vault 
            var vaultCustomer = await _qualpayManager.GetCustomer(customer.Id);

            //prepare model
            var model = new QualpayCustomerModel
            {
                Id = customerModel.Id,
                CustomerExists = vaultCustomer != null,
                QualpayCustomerId = vaultCustomer?.CustomerId,
                HideBlock = await _genericAttributeService.GetAttributeAsync<bool>(await _workContext.GetCurrentCustomerAsync(), QualpayDefaults.HideBlockAttribute)
            };

            //prepare nested search models
            model.CustomerCardSearchModel.CustomerId = customer.Id;
            model.CustomerCardSearchModel.SetGridPageSize();

            return model;
        }

        /// <summary>
        /// Prepare paged Qualpay customer card list model
        /// </summary>
        /// <param name="searchModel">Qualpay customer card search model</param>
        /// <param name="customer">Customer</param>
        /// <returns>Qualpay customer card list model</returns>
        public async Task<QualpayCustomerCardListModel> PrepareQualpayCustomerCardListModel(QualpayCustomerCardSearchModel searchModel, Customer customer)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            if (customer == null)
                throw new ArgumentNullException(nameof(customer));

            //try to get customer billing cards
            var billingCards = (await _qualpayManager.GetCustomerCards(customer.Id)).ToList().ToPagedList(searchModel);

            //prepare list model
            var model = new QualpayCustomerCardListModel().PrepareToGrid(searchModel, billingCards, () =>
            {
                return billingCards.Select(card => new QualpayCustomerCardModel
                {
                    Id = card.CardId,
                    CardId = card.CardId,
                    CardType = card.CardType?.ToString(),
                    ExpirationDate = card.ExpDate,
                    MaskedNumber = card.CardNumber
                });
            });

            return model;
        }

        #endregion
    }
}