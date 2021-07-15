using System;
using FluentValidation;
using Nop.Plugin.Payments.Qualpay.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Qualpay.Validators
{
    /// <summary>
    /// Represents payment info model validator
    /// </summary>
    public class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        #region Ctor

        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            //set validation rules
            RuleFor(model => model.CardholderName)
                .NotEmpty()
                .WithMessage(localizationService.GetResourceAsync("Payment.CardholderName.Required").Result)
                .When(model => string.IsNullOrEmpty(model.BillingCardId) || model.BillingCardId.Equals(Guid.Empty.ToString()));

            RuleFor(model => model.CardNumber)
                .IsCreditCard()
                .WithMessage(localizationService.GetResourceAsync("Payment.CardNumber.Wrong").Result)
                .When(model => string.IsNullOrEmpty(model.BillingCardId) || model.BillingCardId.Equals(Guid.Empty.ToString()));

            RuleFor(model => model.CardCode)
                .Matches(@"^[0-9]{3,4}$")
                .WithMessage(localizationService.GetResourceAsync("Payment.CardCode.Wrong").Result)
                .When(model => string.IsNullOrEmpty(model.BillingCardId) || model.BillingCardId.Equals(Guid.Empty.ToString()));

            RuleFor(model => model.ExpireMonth)
                .NotEmpty()
                .WithMessage(localizationService.GetResourceAsync("Payment.ExpireMonth.Required").Result)
                .When(model => string.IsNullOrEmpty(model.BillingCardId) || model.BillingCardId.Equals(Guid.Empty.ToString()));

            RuleFor(model => model.ExpireYear)
                .NotEmpty()
                .WithMessage(localizationService.GetResourceAsync("Payment.ExpireYear.Required").Result)
                .When(model => string.IsNullOrEmpty(model.BillingCardId) || model.BillingCardId.Equals(Guid.Empty.ToString()));
        }

        #endregion
    }
}