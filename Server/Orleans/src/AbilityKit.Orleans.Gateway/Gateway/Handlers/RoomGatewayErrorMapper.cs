using System.Text;

namespace AbilityKit.Orleans.Gateway.Handlers;

internal static class RoomGatewayErrorMapper
{
    public static GatewayResponse ToResponse(uint seq, Exception exception)
    {
        var error = RoomOperationErrorClassifier.ToError(exception);
        var payload = Encoding.UTF8.GetBytes(error.Message);
        return GatewayResponse.Error(seq, error.GatewayStatusCode, payload);
    }

}
