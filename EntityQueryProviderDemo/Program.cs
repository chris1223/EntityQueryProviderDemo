using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityQueryProviderDemo
{
    public class Customers
    {
        public int ID { get; set; }
        public string NAME { get; set; }
        public int AGE { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SqlConnection conn = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Docs\LINQ\Examples3\LINQ\EntityQueryProviderDemo\EntityQueryProviderDemo\Database1.mdf;Integrated Security=True");
            var context = new EntityContext<Customers>("Customers", conn.CreateCommand());
            foreach (var item in context.Where(a => a.NAME == "code6421"))
                Console.WriteLine(item.NAME);
            Console.ReadLine();
        }
    }
}
