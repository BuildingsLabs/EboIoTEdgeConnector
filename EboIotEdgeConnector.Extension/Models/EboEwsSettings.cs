using System.ComponentModel.DataAnnotations;
using Mongoose.Common.Attributes;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public class EboEwsSettings : ITraversable, IEndpoint
    {
        #region Address - IEndpoint Member
        [Required]
        public string Address { get; set; }
        #endregion
        #region UserName - IEndpoint Member
        [Required]
        public string UserName { get; set; }
        #endregion
        #region Password - IEndpoint Member
        [Required, EncryptedString]
        public string Password { get; set; } 
        #endregion
    }
}