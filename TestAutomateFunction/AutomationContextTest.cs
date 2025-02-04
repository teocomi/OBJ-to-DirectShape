using Objects.BuiltElements.Revit;
using Objects.Geometry;
using Speckle.Automate.Sdk;
using Speckle.Automate.Sdk.Test;
using Speckle.Core.Api;
using Speckle.Core.Api.GraphQL.Models;
using Speckle.Core.Credentials;
using Speckle.Core.Models;
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
      RevitCategory = "Banana",
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

  [Test]
  public void ConvertToDirectShape_InvalidCategory_ReturnsNull()
  {
    var obj = new Base();
    var result = AutomateFunction.ConvertToDirectShape(obj, "InvalidCategory");
    Assert.That(result, Is.Null);
  }

  [Test]
  public void ConvertToDirectShape_ValidCategory_ReturnsDirectShape()
  {
    var obj = new Base();
    obj["displayValue"] = new List<Mesh> { new Mesh() };
    var result = AutomateFunction.ConvertToDirectShape(obj, "Walls");
    Assert.That(result, Is.Not.Null);
    Assert.That(result["category"], Is.EqualTo(RevitCategory.Walls));
  }

  [Test]
  public void ValidateRevitCategory_InvalidCategory_ReturnsGenericModel()
  {
    // Arrange
    var invalidCategory = "Banana";

    // Act
    var result = AutomateFunction.ValidateRevitCategory(invalidCategory);

    // Assert
    Assert.That(result, Is.EqualTo(RevitCategory.GenericModel.ToString()));
  }

  [Test]
  public void ValidateRevitCategory_ValidCategory_ReturnsInputCategory()
  {
    // Arrange
    var validCategory = "Walls";

    // Act
    var result = AutomateFunction.ValidateRevitCategory(validCategory);

    // Assert
    Assert.That(result, Is.EqualTo(RevitCategory.Walls.ToString()));
  }

  [Test]
  public void GenerateTargetModelName_ValidInputs_ReturnsCorrectName()
  {
    var sourceModelName = "Example/Model Name";
    var prefix = "Converted/";

    var result = AutomateFunction.GenerateTargetModelName(sourceModelName, prefix);

    Assert.That(result, Is.EqualTo("Converted/Example/Model_Name"));
  }

  [Test]
  public void GenerateTargetModelName_EmptyPrefix_ThrowsArgumentException()
  {
    var sourceModelName = "Example";

    Assert.Throws<ArgumentException>(
      () => AutomateFunction.GenerateTargetModelName(sourceModelName, "")
    );
  }

  [Test]
  public void GenerateTargetModelName_LongSourceModelName_ThrowsArgumentException()
  {
    var sourceModelName = new string('a', 300); // Excessively long name
    var prefix = "Converted/";

    Assert.Throws<ArgumentException>(
      () => AutomateFunction.GenerateTargetModelName(sourceModelName, prefix)
    );
  }

  [Test]
  public async Task TestFunctionRun_EmptyInputs_FailsGracefully()
  {
    var inputs = new FunctionInputs { RevitCategory = "", TargetModelPrefix = "" };

    var automationRunData = await TestAutomateUtils.CreateTestRun(client);
    var automationContext = await AutomationRunner.RunFunction(
      AutomateFunction.Run,
      automationRunData,
      account.token,
      inputs
    );

    Assert.That(automationContext.RunStatus, Is.EqualTo("EXCEPTION"));
  }

  [Test]
  public void ConvertToDirectShape_NullObject_ReturnsNull()
  {
    var result = AutomateFunction.ConvertToDirectShape(null, "Walls");
    Assert.That(result, Is.Null);
  }

  [Test]
  public void ConvertToDirectShape_EmptyMeshList_ReturnsNull()
  {
    var obj = new Base();
    obj["displayValue"] = new List<Mesh>(); // Empty list

    var result = AutomateFunction.ConvertToDirectShape(obj, "Walls");

    Assert.That(result, Is.Null);
  }

  [Test]
  public void GenerateTargetModelName_SpecialCharactersInPrefix_SanitizedCorrectly()
  {
    const string sourceModelName = "Model";
    const string prefix = "Converted@/";

    var result = AutomateFunction.GenerateTargetModelName(sourceModelName, prefix);

    Assert.That(result, Is.EqualTo("Converted_/Model"));
  }

  [Test]
  public void GenerateTargetModelName_EmptyModelName_ThrowsArgumentException()
  {
    Assert.Throws<ArgumentException>(
      () => AutomateFunction.GenerateTargetModelName(" ", "Converted/")
    );
  }

  [Test]
  public void GenerateTargetModelName_SpecialCharacters_SanitizedCorrectly()
  {
    const string sourceModelName = "Example/Model@Name#Test";
    const string prefix = "Converted/";

    var result = AutomateFunction.GenerateTargetModelName(sourceModelName, prefix);

    Assert.That(result, Is.EqualTo("Converted/Example/Model_Name_Test"));
  }

  public void Dispose()
  {
    client.Dispose();
    TestAutomateEnvironment.Clear();
  }
}
