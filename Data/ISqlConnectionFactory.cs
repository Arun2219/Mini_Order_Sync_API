using System.Data;

namespace Mini_Order_Sync_API.Data;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
