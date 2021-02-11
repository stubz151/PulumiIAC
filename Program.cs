using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Automation;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using System;
using System.Threading.Tasks;
using Account = Pulumi.Azure.Automation.Account;
using AccountArgs = Pulumi.Azure.Automation.AccountArgs;

namespace PulumiIAC
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("starting deployment");
            string rgName = "PulumiTest";
            var resourceGroup = CreateResourceGroup(rgName, Locations.WestEurope);

            var plan = GetAppServicePlan("CoolApps", new PlanArgs 
            { 
                Location = Locations.WestEurope, Kind = AppServiceKind.App,
                Sku = new PlanSkuArgs
                {
                    Tier = AppServiceTier.Basic,
                    Size = "B1"
                }
            });
            var storageAccount = GetStorageAccount(rgName);
            var container = GetContainer(storageAccount.Name, "Messages");
        }

        static ResourceGroup CreateResourceGroup(string rgName, string locationName) => new ResourceGroup(rgName, new ResourceGroupArgs
        {
            Location = locationName,
        });

        static Plan GetAppServicePlan(string planName, PlanArgs args) => new Plan(planName, args);

        static Container GetContainer(string storageAccountName, string containerName) => new Container(containerName, new ContainerArgs
        {
            StorageAccountName = storageAccountName,
            ContainerAccessType = "private",
        });
        static Account GetStorageAccount(string resourceGroupName) => new Account("sa", new AccountArgs
        {
            ResourceGroupName = resourceGroupName
        });
    }
}
