using System;
using DotNetCore.CAP.Messages;
using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.RabbitMQ.MySql.FreeSql
{
    public class Startup
    {
        public IFreeSql Fsql { get; }
        public IConfiguration Configuration { get; }

        private string connectionString;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            connectionString = configuration.GetSection("ConnectString:MySql").Value;

            Fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, connectionString)
                .UseAutoSyncStructure(true)
                .Build();
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(Fsql);
            services.AddCap(x =>
            {

                x.UseMySql(connectionString);

                x.UseRabbitMQ(o =>
                {
                    o.HostName = "localhost";
                    o.UserName = "guest";
                    o.Password = "guest";
                    o.VirtualHost = "/";
                    o.ExchangeName = "cap.cms.topic";

                    o.ConnectionFactoryOptions = opt =>
                    {
                        //rabbitmq client ConnectionFactory config
                    };
                });
                x.UseDashboard();
                x.FailedRetryCount = 5;
                x.FailedThresholdCallback = (type) =>
                {
                    Console.WriteLine(
                        $@"A message of type {type} failed after executing {x.FailedRetryCount} several times, requiring manual troubleshooting. Message name: {type.Message.GetName()}");
                };
            });

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
