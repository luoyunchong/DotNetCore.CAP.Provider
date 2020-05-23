# DotNetCore.CAP.Provider
DotNetCore.CAP  为其增加FreeSql中的统一事务提交，没有减少代码，如对EFCore的依赖，如果需要去掉EFCore的依赖，请查看此项目中[https://github.com/luoyunchong/lin-cms-dotnetcore/tree/master/framework](https://github.com/luoyunchong/lin-cms-dotnetcore/tree/master/framework)中src/IGeekfan.CAP.MySql项目源码。

该项目分为二种方式实现CAP配合FreeSql的分布式事务一致性。


## 1.改源码，为其增加一些扩展，并在switch的地方，加IUnitOfWork的判断。

## Getting Started 
### NuGet 

你可以运行以下下命令在你的项目中安装 CAP。

```
PM> Install-Package DotNetCore.CAP.MySql.Provider
```

安装FreeSql
```
PM> Install-Package FreeSql
PM> Install-Package FreeSql.Provider.MySqlConnector
PM> Install-Package FreeSql.DbContext
```
### Configuration

首先配置CAP到 Startup.cs 文件中，如下：

```c#
    public IFreeSql Fsql { get; }
    public IConfiguration Configuration { get; }
    private string connectionString = @"Data Source=localhost;Port=3306;User ID=root;Password=123456;Initial Catalog=captest;Charset=utf8mb4;SslMode=none;Max pool size=10";
    
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


在控制器中得到
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

## 2. 不修改CAP源码怎么办呢?
另外，如果你不想修改CAP的源码，FreeSql作者叶老板，为我们指一个思路。

按照大佬的思路，可以不改变CAP的代码基础上，通过写一个扩展方法。这样我们就可以仅安装官方提供的包。

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


使用demo,完整项目可查看 https://github.com/luoyunchong/DotNetCore.CAP.Provider/tree/master/samples/Sample.RabbitMQ.MySql.FreeSql
```
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
````

