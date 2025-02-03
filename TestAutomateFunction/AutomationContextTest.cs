using Speckle.Automate.Sdk;
using Speckle.Automate.Sdk.Test;
using Speckle.Core.Api;
using Speckle.Core.Api.GraphQL.Models;
using Speckle.Core.Credentials;
using SpeckleObjToDirectShape;

namespace TestAutomateFunction;

[TestFixture]
public sealed class AutomationContextTest : IDisposable
{
  private Client client;
  private Account account;

  [OneTimeSetUp]
  public void Setup()
  {
    account = new Account
    {
      token = TestAutomateEnvironment.GetSpeckleToken(),
      serverInfo = new ServerInfo
      {
        url = TestAutomateEnvironment.GetSpeckleServerUrl().ToString()
      }
    };
    client = new Client(account);
  }

  [Test]
  public async Task TestFunctionRun()
  {
    var inputs = new FunctionInputs
    {
      RevitCategory = "Walls",
      TargetModelPrefix = "Converted"
    };

    var automationRunData = await TestAutomateUtils.CreateTestRun(client);
    var automationContext = await AutomationRunner.RunFunction(
      AutomateFunction.Run,
      automationRunData,
      account.token,
      inputs
    );

    Assert.That(automationContext.RunStatus, Is.EqualTo("SUCCEEDED"));
  }

  public void Dispose()
  {
    client.Dispose();
    TestAutomateEnvironment.Clear();
  }
}
