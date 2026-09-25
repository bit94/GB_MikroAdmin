using GB.MikroAdmin.Models;
using GB.MikroAdmin.Services;
using Xunit;
namespace GB.MikroAdmin.Tests;
public sealed class TikFakeConnectionTests {
 [Fact]
 public async Task AddUser_appears_with_the_requested_limit() {
  await using var router=new TikFakeConnection();
  await router.ConnectAsync(new("router",8729,"admin","secret",true,true),CancellationToken.None);
  await router.AddUserAsync("cliente-1","no-se-expone","limitado",TimeSpan.FromHours(1),CancellationToken.None);
  var user=Assert.Single((await router.UsersAsync(CancellationToken.None)).Where(x=>x.Name=="cliente-1"));
  Assert.Equal(TimeSpan.FromHours(1),user.LimitUptime);
 }
 [Fact]
 public async Task RemoveUser_removes_the_selected_account() {
  await using var router=new TikFakeConnection();
  await router.AddUserAsync("cliente-2","secret","limitado",TimeSpan.FromHours(1),CancellationToken.None);
  await router.RemoveUserAsync("cliente-2",CancellationToken.None);
  Assert.DoesNotContain((await router.UsersAsync(CancellationToken.None)),x=>x.Name=="cliente-2");
 }
}
