using System;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Task types that can run without depending on a specific chip family.
    /// Use these values to drive generic pre-checks such as UID validation.
    /// </summary>
    public enum TaskType_GenericChipTask
    {
        None,
        ChipIsOfType,
        ChipIsMultiChip,
        CheckUID,
        ChangeDefault
    }

    /// <summary>
    /// Task types that relate to application-wide functionality such as reporting
    /// or running helper programs.
    /// </summary>
    public enum TaskType_CommonTask
    {
        None,
        CreateReport,
        CheckLogicCondition,
        ExecuteProgram,
        ChangeDefault
    }

    /// <summary>
    /// Tasks that can be executed against a MIFARE Classic chip.
    /// </summary>
    public enum TaskType_MifareClassicTask
    {
        None,
        ReadData,
        WriteData,
        EmptyCheck,
        ChangeDefault
    }

    /// <summary>
    /// Tasks that can be executed against a MIFARE Ultralight chip.
    /// </summary>
    public enum TaskType_MifareUltralightTask
    {
        None,
        ReadData,
        WriteData,
        ChangeDefault
    }

    /// <summary>
    /// Tasks that can be executed against a MIFARE DESFire chip. Use these values to drive
    /// the DESFire task wizard and to dispatch the right reader operations.
    /// </summary>
    public enum TaskType_MifareDesfireTask
    {
        None,
        FormatDesfireCard,
        PICCMasterKeyChangeover,
        CreateApplication,
        DeleteApplication,
        ApplicationKeyChangeover,
        ApplicationKeySettingsChangeover,
        CreateFile,
        DeleteFile,
        ReadData,
        WriteData,
        AppExistCheck,
        AuthenticateApplication,
        ReadAppSettings,
        ChangeDefault
    }
}
