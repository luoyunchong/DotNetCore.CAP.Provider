using System;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using Sample.RabbitMQ.MySql.FreeSql.Models;

namespace Sample.RabbitMQ.MySql.FreeSql.Controllers
{
    public class FreeSqlController : Controller
    {
        private readonly ICapPublisher _capBus;
        private readonly IFreeSql _freeSql;

        public FreeSqlController(ICapPublisher capPublisher, IFreeSql freeSql)
        {
            _capBus = capPublisher;
            _freeSql = freeSql;
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
                    Summary = "summary" + (id == 1
                        ? "summarysummarysummarysummarysummarysummarysummarysummarysummarysummarysummary"
                        : ""),
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


        [CapSubscribe("FreeSqlController.time")]
        public void GetTime(DateTime time)
        {
            Console.WriteLine($"time:{time}");
        }
    }
}