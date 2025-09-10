namespace DeviceSpecAnalyzer.Core.Models;

public enum MessageDirection
{
    DeviceToSystem,
    SystemToDevice,
    Bidirectional
}

public enum MessageCategory
{
    BasicProfile,
    Directive,
    VendorSpecific,
    Unknown
}

public enum DocumentType
{
    Unknown,
    Specification,
    TraceLog,
    Mixed
}

public class MessageProfile
{
    public string MessageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MessageDirection Direction { get; set; }
    public MessageCategory Category { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public List<string> KeyFields { get; set; } = new();
    public string Example { get; set; } = string.Empty;
    public bool IsConversationStarter { get; set; }
    public bool RequiresAcknowledgment { get; set; }
    public List<string> RelatedMessages { get; set; } = new();
}

public class ParsedMessage
{
    public string MessageType { get; set; } = string.Empty;
    public ProtocolType Protocol { get; set; }
    public List<MessageSegment> Segments { get; set; } = new();
    public string RawContent { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public MessageProfile? Profile { get; set; }
    public DateTime? MessageDateTime { get; set; }
    public string? ControlId { get; set; }
    public int SequenceNumber { get; set; }
    public Dictionary<string, string> KeyValues { get; set; } = new();
    
    // Properties for specification document support
    public bool IsSpecificationExample { get; set; }
    public List<ParsedMessage> Examples { get; set; } = new();
    public int ExampleCount => Examples.Count;
    public string? ExampleContext { get; set; } // Page number, section reference, etc.
}

public class DocumentParsingResult
{
    public DocumentType DocumentType { get; set; } = DocumentType.Unknown;
    public List<ParsedMessage> Messages { get; set; } = new();
    public List<ParsedMessage> MessageTypes { get; set; } = new(); // Unique message types for specifications
    public int TotalExamples { get; set; }
    public Dictionary<string, int> MessageTypeCounts { get; set; } = new();
    public string AnalysisSummary { get; set; } = string.Empty;
}

public class MessageSegment
{
    public string SegmentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MessageField> Fields { get; set; } = new();
    public string RawSegment { get; set; } = string.Empty;
}

public class MessageField
{
    public string FieldId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string DataType { get; set; } = string.Empty;
}

public static class MessageDefinitions
{
    public static readonly Dictionary<string, MessageProfile> POCT1A_MessageProfiles = new()
    {
        ["HEL.R01"] = new MessageProfile
        {
            MessageId = "HEL.R01",
            Name = "Hello Message",
            Description = "Initiate communication",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.BasicProfile,
            Purpose = "Initiates communication and provides device identification including vendor ID, device name, software version, and supported capabilities",
            KeyFields = new() { "HDR.control_id", "DEV.device_id", "DEV.sw_version", "DEV.manufacturer_name" },
            IsConversationStarter = true,
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["ACK.R01"] = new MessageProfile
        {
            MessageId = "ACK.R01",
            Name = "Message ACK",
            Description = "Message acknowledgement response",
            Direction = MessageDirection.Bidirectional,
            Category = MessageCategory.BasicProfile,
            Purpose = "Acknowledges receipt of messages and indicates success or failure of processing",
            KeyFields = new() { "ACK.type_cd", "ACK.ack_control_id" },
            RequiresAcknowledgment = false
        },
        ["DST.R01"] = new MessageProfile
        {
            MessageId = "DST.R01",
            Name = "Device Status",
            Description = "Device status message",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.BasicProfile,
            Purpose = "Reports current device status including new observation counts, device events, and instrument condition (ready/locked)",
            KeyFields = new() { "DST.new_observations_qty", "DST.new_events_qty", "DST.condition_cd" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["OBS.R01"] = new MessageProfile
        {
            MessageId = "OBS.R01",
            Name = "Patient Observation Data",
            Description = "Patient observation data",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.BasicProfile,
            Purpose = "Transmits patient test results including analyte values, patient ID, operator, and reagent information",
            KeyFields = new() { "PT.patient_id", "OBS.observation_id", "OBS.value", "SVC.observation_dttm" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "REQ.R01", "ACK.R01" }
        },
        ["OBS.R02"] = new MessageProfile
        {
            MessageId = "OBS.R02",
            Name = "Control Observation Data", 
            Description = "Control observation data",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.BasicProfile,
            Purpose = "Transmits quality control test results including control lot information, expected ranges, and QC values",
            KeyFields = new() { "CTC.name", "CTC.lot_number", "OBS.value", "OBS.normal_lo_hi_limit" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "REQ.R01", "ACK.R01" }
        },
        ["REQ.R01"] = new MessageProfile
        {
            MessageId = "REQ.R01",
            Name = "Request Data",
            Description = "Request data",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.BasicProfile,
            Purpose = "Requests specific data from device (new observations, all observations, device events, or device status)",
            KeyFields = new() { "REQ.request_cd" },
            RequiresAcknowledgment = false,
            RelatedMessages = new() { "OBS.R01", "OBS.R02", "EVS.R01", "DST.R01" }
        },
        ["END.R01"] = new MessageProfile
        {
            MessageId = "END.R01",
            Name = "Terminate Conversation",
            Description = "Terminate conversation",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.BasicProfile,
            Purpose = "Terminates the current conversation session with the device",
            KeyFields = new() { "TRM.reason_cd" },
            RequiresAcknowledgment = false
        },
        ["ESC.R01"] = new MessageProfile
        {
            MessageId = "ESC.R01",
            Name = "Escape Message",
            Description = "Escape message",
            Direction = MessageDirection.Bidirectional,
            Category = MessageCategory.BasicProfile,
            Purpose = "Indicates device cannot process incoming messages (e.g., assay running)",
            KeyFields = new() { "ESC.detail_cd", "ESC.note_txt" },
            RequiresAcknowledgment = false
        },
        ["EVS.R01"] = new MessageProfile
        {
            MessageId = "EVS.R01",
            Name = "Device Events",
            Description = "Device event information and errors",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.BasicProfile,
            Purpose = "Reports device events and error conditions including patient context and assay information",
            KeyFields = new() { "EVT.description", "EVT.severity_cd", "EVT.event_dttm", "EVT.assay_type" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "REQ.R01", "ACK.R01" }
        },
        ["DTV.R01"] = new MessageProfile
        {
            MessageId = "DTV.R01",
            Name = "Simple Directive",
            Description = "Simple directive",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.Directive,
            Purpose = "Sends simple commands to device without additional data",
            KeyFields = new() { "DTV.command_cd" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["DTV.R02"] = new MessageProfile
        {
            MessageId = "DTV.R02",
            Name = "Complex Directive",
            Description = "Complex directive with additional data",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.Directive,
            Purpose = "Sends complex commands with additional data (e.g., set time, operator lists)",
            KeyFields = new() { "DTV.command_cd" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["OPL.R01"] = new MessageProfile
        {
            MessageId = "OPL.R01",
            Name = "New Operator List",
            Description = "Complete operator list update",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.Directive,
            Purpose = "Replaces entire operator list in device with new operators and permissions",
            KeyFields = new() { "OPR.operator_id", "ACC.method_cd", "ACC.permission_level_cd" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01", "EOT.R01" }
        },
        ["OPL.R02"] = new MessageProfile
        {
            MessageId = "OPL.R02",
            Name = "Incremental Operator List",
            Description = "Incremental operator list update",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.Directive,
            Purpose = "Adds or removes specific operators from device without replacing entire list",
            KeyFields = new() { "UPD.action_cd", "OPR.operator_id" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["DTV.ALERE.AXIS.LQCSET"] = new MessageProfile
        {
            MessageId = "DTV.ALERE.AXIS.LQCSET",
            Name = "Liquid QC Lot Setup",
            Description = "Liquid control lot information Add/clear list",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.VendorSpecific,
            Purpose = "Manages liquid QC control lot information including expected ranges and expiration dates",
            KeyFields = new() { "name", "lot_number", "level_cd", "expiration_date" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["DTV.ALERE.AXIS.DVCSET"] = new MessageProfile
        {
            MessageId = "DTV.ALERE.AXIS.DVCSET", 
            Name = "Device Setup",
            Description = "Device setup configuration",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.VendorSpecific,
            Purpose = "Configures device settings including QC lockout, operator lockout, and connection timing",
            KeyFields = new() { "operator_lockout", "qc_lockout", "connection.connect_dly" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01" }
        },
        ["ALERE.AXIS.LOCKSTATUS"] = new MessageProfile
        {
            MessageId = "ALERE.AXIS.LOCKSTATUS",
            Name = "Lockout Status",
            Description = "Lockout status information",
            Direction = MessageDirection.DeviceToSystem,
            Category = MessageCategory.VendorSpecific,
            Purpose = "Reports current lockout status for QC and operator restrictions",
            KeyFields = new() { "qc_lockout", "operator_lockout" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "REQ.R01" }
        },
        ["DTV.AFINION.SWU"] = new MessageProfile
        {
            MessageId = "DTV.AFINION.SWU",
            Name = "Software Upgrade Directive",
            Description = "Software upgrade directive",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.VendorSpecific,
            Purpose = "Initiates software upgrade process with image size and segment information",
            KeyFields = new() { "SWU.image_size", "SWU.segment_size", "SWU.auto_update" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01", "SWU.SEGMENT" }
        },
        ["SWU.SEGMENT"] = new MessageProfile
        {
            MessageId = "SWU.SEGMENT",
            Name = "Software Upgrade Segment", 
            Description = "Software upgrade segment data",
            Direction = MessageDirection.SystemToDevice,
            Category = MessageCategory.VendorSpecific,
            Purpose = "Transmits individual segments of software upgrade image with checksums",
            KeyFields = new() { "SEG.size", "SEG.seq", "SEG.crc" },
            RequiresAcknowledgment = true,
            RelatedMessages = new() { "ACK.R01", "EOT.R01" }
        }
    };

    // Legacy compatibility - maintain existing structure
    public static readonly Dictionary<string, (string Name, string Description)> POCT1A_Messages = 
        POCT1A_MessageProfiles.ToDictionary(kv => kv.Key, kv => (kv.Value.Name, kv.Value.Description));

    public static readonly Dictionary<string, (string Name, string Description)> ASTM_Segments = new()
    {
        ["H"] = ("Header", "Message header with analyzer identification"),
        ["P"] = ("Patient", "Patient identification information"), 
        ["O"] = ("Order", "Test order and sample information"),
        ["C"] = ("Comment", "Additional comments and sample information"),
        ["R"] = ("Result", "Test result data and values"),
        ["L"] = ("Terminator", "Message termination marker")
    };

    public static readonly Dictionary<string, List<(string Id, string Name, string Description, bool Required)>> POCT1A_Fields = new()
    {
        ["HEL.R01"] = new()
        {
            ("HDR.control_id", "Control ID", "Unique message identifier", true),
            ("HDR.version_id", "Version ID", "Protocol version (POCT1)", true),
            ("HDR.creation_dttm", "Creation DateTime", "Message creation timestamp", true),
            ("DEV.device_id", "Device ID", "Device MAC address", true),
            ("DEV.serial_id", "Serial Number", "Device serial number", true),
            ("DEV.manufacturer_name", "Manufacturer", "Device manufacturer name", true),
            ("DEV.device_name", "Device Name", "Device model name", true),
            ("DEV.sw_version", "Software Version", "Device software version", true)
        },
        ["OBS.R01"] = new()
        {
            ("HDR.control_id", "Control ID", "Unique message identifier", true),
            ("SVC.role_cd", "Role Code", "Service role (OBS for observations)", true),
            ("SVC.observation_dttm", "Observation DateTime", "When test was completed", true),
            ("PT.patient_id", "Patient ID", "Patient identifier", true),
            ("OBS.observation_id", "Observation ID", "Analyte name", true),
            ("OBS.qualitative_value", "Result Value", "Test result (positive/negative)", true),
            ("OPR.operator_id", "Operator ID", "Operator who performed test", false),
            ("RGT.name", "Reagent Name", "Test cartridge/reagent name", true),
            ("RGT.lot_number", "Lot Number", "Reagent lot number", true)
        }
    };

    public static readonly Dictionary<string, List<(string Id, string Name, string Description, bool Required)>> ASTM_Fields = new()
    {
        ["H"] = new()
        {
            ("H-1", "Record Type", "Always 'H' for header", true),
            ("H-2", "Delimiters", "Field delimiters (|\\^&)", true),
            ("H-5.1", "Analyzer Name", "Device name", true),
            ("H-5.2", "Serial Number", "Device serial number", true),
            ("H-12", "Processing ID", "Always 'P' for production", true),
            ("H-13", "Version", "Software version", true),
            ("H-14", "DateTime", "Message creation time", true)
        },
        ["R"] = new()
        {
            ("R-1", "Record Type", "Always 'R' for result", true),
            ("R-2", "Sequence Number", "Result sequence number", true),
            ("R-3", "Analyte Name", "Test analyte identifier", true),
            ("R-4", "Result Value", "Test result value", true),
            ("R-7", "Test Flag", "Result interpretation flag", false),
            ("R-9", "Result Type", "F=Final, R=Retransmitted", true),
            ("R-13", "DateTime", "Test completion time", true)
        }
    };
}