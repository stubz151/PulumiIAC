using Pulumi;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Automation;
using Pulumi.Azure.Core;
using Pulumi.Azure.Sql;
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
            string storageAccountName = "sa";

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

            var storageAccount = GetStorageAccount(rgName, storageAccountName);

            var container = GetContainer(storageAccountName, "Messages");

            var appInsights = new Insights("appInsights", new InsightsArgs
            {
                ApplicationType = "web",
                ResourceGroupName = rgName
            });

            var config = new Config();
            var username = config.Get("sqlAdmin") ?? "pulumi";
            var password = config.RequireSecret("sqlPassword");

            var blob = new Blob("messages", new BlobArgs
            {
                StorageAccountName = storageAccount.Name,
                StorageContainerName = container.Name,
                Type = "Block",
                Source = new FileArchive("wwwroot"),
            });

            var codeBlobUrl = SharedAccessSignature.SignedBlobReadUrl(blob, storageAccount);

            var sqlServer = new SqlServer("sql", new SqlServerArgs
            {
                ResourceGroupName = rgName,
                AdministratorLogin = username,
                AdministratorLoginPassword = password,
                Version = "12.0",
            });

            var database = new Database("db", new DatabaseArgs
            {
                ResourceGroupName = rgName,
                ServerName = sqlServer.Name,
                RequestedServiceObjectiveName = "S0",
            });

            var app = new AppService("app", new AppServiceArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AppServicePlanId = plan.Id,
                AppSettings =
            {
                {"WEBSITE_RUN_FROM_PACKAGE", codeBlobUrl},
                {"APPINSIGHTS_INSTRUMENTATIONKEY", appInsights.InstrumentationKey},
                {"APPLICATIONINSIGHTS_CONNECTION_STRING", appInsights.InstrumentationKey.Apply(key => $"InstrumentationKey={key}")},
                {"ApplicationInsightsAgent_EXTENSION_VERSION", "~2"},
            },
                ConnectionStrings =
            {
                new AppServiceConnectionStringArgs
                {
                    Name = "db",
                    Type = "SQLAzure",
                    Value = Output.Tuple<string, string, string>(sqlServer.Name, database.Name, password).Apply(t =>
                    {
                        (string server, string database, string pwd) = t;
                        return
                            $"Server= tcp:{server}.database.windows.net;initial catalog={database};userID={username};password={pwd};Min Pool Size=0;Max Pool Size=30;Persist Security Info=true;";
                    }),
                },
            },
            });

            this.Endpoint = app.DefaultSiteHostname;

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
        static Account GetStorageAccount(string resourceGroupName, string storageAccountName) => new Account(storageAccountName, new AccountArgs
        {
            ResourceGroupName = resourceGroupName
        });
    }
}
