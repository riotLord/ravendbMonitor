using Microsoft.Azure.Functions.Worker;
namespace Servercyde.Monitoring.Tests.Fakes;

public class FakeFunctionContext(
    IServiceProvider serviceProvider,
    string invocationId
) : FunctionContext()
{
    public override string InvocationId => invocationId;
    public override IServiceProvider InstanceServices { get; set; } = serviceProvider;

    #region NotImplemented
    public override string FunctionId => throw new NotImplementedException();

    public override TraceContext TraceContext => throw new NotImplementedException();

    public override BindingContext BindingContext => throw new NotImplementedException();

    public override RetryContext RetryContext => throw new NotImplementedException();

    public override FunctionDefinition FunctionDefinition => throw new NotImplementedException();

    public override IDictionary<object, object> Items { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override IInvocationFeatures Features => throw new NotImplementedException();
    #endregion
}
