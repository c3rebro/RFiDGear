using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public abstract class GenericTaskModel
    {
        public GenericTaskModel()
        {
        }

        public GenericTaskModel(ERROR sourceErrorLevel, ERROR targetErrorLevel)
        {
            TargetErrorLevel = targetErrorLevel;
            SourceErrorLevel = sourceErrorLevel;
        }

        public abstract ERROR TargetErrorLevel { get; set; }

        public abstract ERROR SourceErrorLevel { get; set; }
    }
}