
using Microsoft.EntityFrameworkCore;

using (var context = new efcoreContext())
{
    //var entity = context.SysUsers;
    //entity.Add(new SysUser()
    //{
    //    Id = "123",
    //    Name = "测试"
    //});
    //var count = context.SaveChanges();
    //Console.WriteLine("Affect Rows Count is:" + count);
    context.MigrationForSqlServer().Migrate();
}


