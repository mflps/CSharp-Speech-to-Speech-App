using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MT.Api.Protocols.SpeechTranslation.DetectAndTranslate
{
    /// <summary>
    /// Types of text results: "partial" or "final".
    /// </summary>
    /// 

    //This class includes 4 DataContract definitions

    [DataContract]
    public enum ResultType 
    {
        [EnumMember(Value="final")] //Value property of the enumMemberAttribute
        Final,
        [EnumMember(Value="partial")]
        Partial 
    }

    /// <summary>
    /// Defines result data shared by partial results and final results.
    /// </summary>
    [DataContract]
    public abstract class ResultMessage
    {
        /// Identifies the type of result: "final" or "partial".
        [DataMember(Name = "type")]
        [JsonConverter(typeof(StringEnumConverter))] //datacontract member with a typeof qualifier
        public ResultType Type;
        /// Result identifier.
        [DataMember(Name = "id")]
        public string Id;
        /// Detected language.
        [DataMember(Name = "detectedLanguage")]
        public string DetectedLanguage;
        /// Transcriptions in the languages of the session. Results are provided in a dictionary
        /// where a key is a language code and the associated value the transcription in this
        /// language.
        [DataMember(Name = "utterances", EmitDefaultValue = false)]
        public Dictionary<string, string> Transcriptions;
    }

    /// <summary>
    /// Defines a final result.
    /// </summary>
    [DataContract]
    public class FinalResultMessage : ResultMessage
    {
        public FinalResultMessage() { this.Type = ResultType.Final; }
    }

    /// <summary>
    /// Defines a partial result.
    /// </summary>
    [DataContract]
    public class PartialResultMessage : ResultMessage
    {
        public PartialResultMessage() { this.Type = ResultType.Partial; }
    }

}
