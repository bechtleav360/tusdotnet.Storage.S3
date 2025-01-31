using tusdotnet.Interfaces;

namespace tusdotnet.Stores.S3.Interfaces;

/// <summary>
/// 
/// </summary>
public interface ITusS3Store : ITusPipelineStore,
    ITusCreationStore,
    ITusReadableStore,
    ITusTerminationStore,
    ITusExpirationStore,
    ITusCreationDeferLengthStore
{
}