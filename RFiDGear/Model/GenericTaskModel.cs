﻿using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// This Interface contains a Description of Common Properties for all Tasks. A Task has an Execute Condition and a Result.
    /// </summary>
    public interface IGenericTaskModel
    {
        /// <summary>
        /// Indicates a Task Result and is interpreted by one of the following ERROR states: true = NoError, null = Empty, false = Everything else
        /// </summary>
        bool? IsTaskCompletedSuccessfully { get; set; }

        /// <summary>
        /// The Condition of Type "ERRORLEVEL" under which an Task is Executed
        /// </summary>
        ERROR SelectedExecuteConditionErrorLevel { get; set; }

        /// <summary>
        /// The Conditions Position in the Collection as "string"
        /// </summary>
        string SelectedExecuteConditionTaskIndex { get; set; }

        /// <summary>
        /// The Errorlevel of the Current Task after it is Executed
        /// </summary>
        ERROR CurrentTaskErrorLevel { get; set; }

        /// <summary>
        /// The Index of the Current Task as String
        /// </summary>
        string CurrentTaskIndex { get; set; }

        /// <summary>
        /// The Index of the Current Task as Integer
        /// </summary>
        int SelectedTaskIndexAsInt { get; }
    }
}