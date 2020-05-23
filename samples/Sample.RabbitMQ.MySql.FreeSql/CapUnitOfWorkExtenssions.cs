using System.Diagnostics;
using System.Reflection;
using DotNetCore.CAP;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.RabbitMQ.MySql.FreeSql
{
    public static class CapExtensions
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
        
        public static void CommitFlush(this IUnitOfWork unitOfWork,ICapTransaction capTransaction)
        {
            unitOfWork.Commit();
            capTransaction.Flush();
        }
    }

}
