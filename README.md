**English** | [简体中文](README.zh-CN.md)

# DotNetCore.CAP.Provider
This project adds coordinated FreeSql transaction commits to DotNetCore.CAP. The original notes that this approach does not remove code or dependencies such as EF Core.

The project demonstrates two approaches for distributed transaction consistency between CAP and FreeSql.

## 1. Extend the CAP source and check for IUnitOfWork in the switch

## Getting Started 
### NuGet 

Install CAP in your project:

```
PM> Install-Package DotNetCore.CAP.MySql.Provider
```

Install FreeSql
```
PM> Install-Package FreeSql
PM> Install-Package FreeSql.Provider.MySqlConnector
PM> Install-Package FreeSql.DbContext
```
### Configuration

First, configure CAP in Startup.cs:

```c#
    public IFreeSql Fsql { get; }
    public IConfiguration Configuration { get; }
    private string connectionString = @"Data Source=localhost;Port=3306;User ID=root;Password=<YOUR_DEV_SECRET>;Initial Catalog=captest;Charset=utf8mb4;SslMode=none;Max pool size=10";
    
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;

        Fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.MySql, connectionString)
            .UseAutoSyncStructure(true)
            .Build();
    }
    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFreeSql>(Fsql);
        services.AddCap(x =>
        {
            x.UseMySql(connectionString);
            x.UseRabbitMQ("localhost");
            x.UseDashboard();
            x.FailedRetryCount = 5;
            x.FailedThresholdCallback = (type, msg) =>
            {
                Console.WriteLine(
                    $@"A message of type {type} failed after executing {x.FailedRetryCount} several times, requiring manual troubleshooting. Message name: {msg.GetName()}");
            };
        });

        services.AddControllers();
    }
```


Use it in a controller:
```
private readonly IFreeSql _freeSql;
private readonly ICapPublisher _capBus;
public TestController(IFreeSql freeSql, ICapPublisher capBus)
{
    _freeSql = freeSql;
    _capBus = capBus;
}

[HttpGet("~/freesql/transaction")]
public DateTime GetTime3()
{
    DateTime now = DateTime.Now;
    using (var uow = _freeSql.CreateUnitOfWork())
    {
        using ICapTransaction trans = uow.BeginTransaction(_capBus, false);
        var repo = uow.GetRepository<WeatherForecast>();

        repo.Insert(new WeatherForecast()
        {
            Date = now,
            Summary = "summary",
            TemperatureC = 100
        });

        repo.Insert(new WeatherForecast()
        {
            Date = now,
            Summary = "11222",
            TemperatureC = 200
        });
        _capBus.Publish("freesql.time", now);
        trans.Commit();
    }

    return now;
}

[CapSubscribe("freesql.time")]
public void GetTime(DateTime time)
{
    Console.WriteLine($"time:{time}");
}
```

## 2. Use CAP without changing its source
If you do not want to modify CAP's source, the FreeSql author suggested another approach.

Implement an extension method without changing CAP, using only the official packages:

```
> Install-Package DotNetCore.CAP.Dashboard
> Install-Package DotNetCore.CAP.MySql
> Install-Package DotNetCore.CAP.RabbitMQ
> Install-Package FreeSql
> Install-Package FreeSql.DbContext
> Install-Package FreeSql.Provider.MySqlConnector
```
```
  public static class CapUnitOfWorkExtensions
    {
        public static ICapTransaction  BeginTransaction(this IUnitOfWork unitOfWork, ICapPublisher publisher, bool autoCommit = false)
        {
            publisher.Transaction.Value = publisher.ServiceProvider.GetService<ICapTransaction>();
            return publisher.Transaction.Value.Begin(unitOfWork.GetOrBeginTransaction(), autoCommit);
        }
        
        public static void Flush(this ICapTransaction capTransaction)
        {
            capTransaction?.GetType().GetMethod("Flush", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(capTransaction, null);
        }
        
        public static void Commit(this IUnitOfWork unitOfWork,ICapTransaction capTransaction)
        {
            unitOfWork.Commit();
            capTransaction.Flush();
        }
    }
```


Usage example; see the complete project at https://github.com/luoyunchong/DotNetCore.CAP.Provider/tree/master/samples/Sample.RabbitMQ.MySql.FreeSql
```
[HttpGet("~/freesql/flush/{id}")]
public DateTime Flush(int id = 0)
{
    DateTime now = DateTime.Now;
    using (var uow = _freeSql.CreateUnitOfWork())
    {
        //Do not use using here: disposing this transaction before uow.Dispose() causes FreeSql to report that the transaction has already been disposed.
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
````

