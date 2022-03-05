using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class GenericTaskModel
    {
        public GenericTaskModel()
        {
        }

        public GenericTaskModel(ERROR sourceErrorLevel, ERROR targetErrorLevel)
        {
            TargetErrorLevel = targetErrorLevel;
            SourceErrorLevel = sourceErrorLevel;
        }
        public ERROR TargetErrorLevel { get; set; }

        public ERROR SourceErrorLevel { get; set; }
    }
}