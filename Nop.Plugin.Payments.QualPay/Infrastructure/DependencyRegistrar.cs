using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Payments.Qualpay.Factories;
using Nop.Plugin.Payments.Qualpay.Services;

namespace Nop.Plugin.Payments.Qualpay.Infrastructure
{
    /// <summary>
    /// Represents a plugin dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>

        public void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            services.AddScoped<QualpayManager>();

            //override services
            services.AddScoped<QualpayCustomerModelFactory>();
        }

        /// <summary>
        /// Gets order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}