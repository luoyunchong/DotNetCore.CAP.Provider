using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Sample.RabbitMQ.MySql.FreeSql.Models;

namespace Sample.RabbitMQ.MySql.FreeSql.Controllers
{
    public class FreeSqlController : Controller
    {
        private readonly ICapPublisher _capBus;
        private readonly IFreeSql _freeSql;
        private readonly IConfiguration configuration;
        public FreeSqlController(ICapPublisher capPublisher, IFreeSql freeSql, IConfiguration configuration)
        {
            _capBus = capPublisher;
            _freeSql = freeSql;
            this.configuration = configuration;
        }

        [Route("~/without/transaction")]
        public async Task<IActionResult> WithoutTransaction()
        {
            await _capBus.PublishAsync("sample.rabbitmq.mysql", new WeatherForecast());

            return Ok();
        }

        [Route("~/adonet/transaction")]
        public IActionResult AdonetWithTransaction()
        {
            using (var connection = new MySqlConnection(configuration.GetSection("ConnectString:MySql").Value))
            {
                using (var transaction = connection.BeginTransaction(_capBus, true))
                {
                    //your business code
                    connection.Execute("insert into test(name) values('test')", transaction: (IDbTransaction)transaction.DbTransaction);

                    //for (int i = 0; i < 5; i++)
                    //{
                    _capBus.Publish("sample.rabbitmq.mysql", DateTime.Now);
                    //}
                }
            }

            return Ok();
        }

        /// <summary>
        /// freesql，配合DotNetCore.CAP.MySql+ CapUnitOfWorkExtenssions中的扩展方法
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("~/freesql/flush/{id}")]
        public DateTime Flush(int id = 0)
        {
            DateTime now = DateTime.Now;
            using (var uow = _freeSql.CreateUnitOfWork())
            {
                //这个不能使用using，因为这个using掉，uow.Dispose()时就会导致FreeSql，提示cannot access dispose transaction
                ICapTransaction trans = uow.BeginTransaction(_capBus, false);
                var repo = uow.GetRepository<WeatherForecast>();
                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "summary" + (id == 1? "summarysummarysummarysummarysummarysummarysummarysummarysummarysummarysummary" : ""),
                    TemperatureC = 100
                });

                if (id == 0)
                {
                    throw new Exception("异常，事务不正常!!");
                }

                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "11222",
                    TemperatureC = 200
                });

                _capBus.Publish("FreeSqlController.time", now);

                uow.Commit(trans);
            }

            return now;
        }

        
        [NonAction]
        [CapSubscribe("FreeSqlController.time")]
        public void GetTime(DateTime time)
        {
            Console.WriteLine($"time:{time}");
        }

        [NonAction]
        [CapSubscribe("sample.rabbitmq.mysql")]
        public void Subscriber(DateTime p)
        {
            Console.WriteLine($@"{DateTime.Now} Subscriber invoked, Info: {p}");
        }
    }
}