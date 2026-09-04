using Npgsql;

namespace SecureFlow.Modules.Organizations.Application.Concurrency;

internal static class PostgresConcurrencyDetector
{
    public static bool IsSerializationFailure(Exception exception)
    {
        if (exception is AggregateException aggregate
            && aggregate.Flatten().InnerExceptions.Any(IsSerializationFailure))
        {
            return true;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected,
                })
            {
                return true;
            }
        }

        return false;
    }
}
