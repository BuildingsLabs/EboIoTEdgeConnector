using System.ComponentModel.DataAnnotations;
using Mongoose.Common.Attributes;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public class IotHubSettings : ITraversable
    {
        #region IotHubName
        [Required]
        public string IotHubName { get; set; }
        #endregion
        #region AzureConnectionString
        [Required, EncryptedString]
        public string AzureConnectionString { get; set; } 
        #endregion
    }
}
