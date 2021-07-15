using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Qualpay.Models.Customer
{
    /// <summary>
    /// Represents Qualpay customer card list model
    /// </summary>
    public record QualpayCustomerCardListModel : BasePagedListModel<QualpayCustomerCardModel>
    {
    }
}