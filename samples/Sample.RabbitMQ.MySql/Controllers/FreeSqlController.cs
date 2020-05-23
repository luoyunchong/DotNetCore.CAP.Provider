using System;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using Sample.RabbitMQ.MySql.Models;

namespace Sample.RabbitMQ.MySql.Controllers
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
        /// 事务测试
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [HttpGet("~/freesql/unitofwork/{id}")]
        public DateTime UnitOfWork(int id=0)
        {
            DateTime now = DateTime.Now;
            using (var uow = _freeSql.CreateUnitOfWork())
            {
                var repo = uow.GetRepository<WeatherForecast>();
                
                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "summary",
                    TemperatureC = 100
                });
                if (id == 0)
                {
                    throw  new Exception("异常，事务不正常!!");
                }
                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "11222",
                    TemperatureC = 200
                });
                uow.Commit();
            }

            return now;
        }
        
        /// <summary>
        /// freesql，配合DotNetCore.CAP.MySql.Provider,id为0，事务异常，CAP消息和FreeSql无法提交。id为1,freesql内部数据库异常，看二者是否回滚。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [HttpGet("~/freesql/transaction/{id}")]
        public DateTime Transaction(int id=0)
        {
            DateTime now = DateTime.Now;
            using (var uow = _freeSql.CreateUnitOfWork())
            {
                using ICapTransaction trans = uow.BeginTransaction(_capBus, false);
                var repo = uow.GetRepository<WeatherForecast>();

                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "summary"+(id==1?"summarysummarysummarysummarysummarysummarysummarysummarysummarysummarysummary":""),
                    TemperatureC = 100
                });
                _capBus.Publish("FreeSqlController.time", now);
                if (id == 0)
                {
                    throw  new Exception("异常，事务不正常!!");
                }
                repo.Insert(new WeatherForecast()
                {
                    Date = now,
                    Summary = "11222",
                    TemperatureC = 200
                });
                trans.Commit();
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