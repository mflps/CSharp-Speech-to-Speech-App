using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MT.Api
{
    //
    // Messages from Server to Client 
    //

    /// <summary>
    /// Defines a partial result.
    /// </summary>
    [DataContract]
    public class PartialResultMessage
    {
        /// Message type identifier.
        [DataMember(Name = "type")]
        public string Type = "partial";
        /// Partial result "major.minor" identifier (e.g. "23.4").
        [DataMember(Name = "id")]
        public string Id;
        /// Recognized text.
        [DataMember(Name = "recognition")]
        public string Recognition;
        /// Translation of the recognized text.
        [DataMember(Name = "translation", EmitDefaultValue = false)]
        public string Translation;
    }

    /// <summary>
    /// Defines a character span with a set of alternative recognitions.
    /// </summary>
    [DataContract]
    public class Span
    {
        /// Zero-based starting character position of the span in the recognition.
        [DataMember(Name= "start")]
        public int Start;
        /// Zero-based ending character position of the span in the recognition.
        [DataMember(Name = "end")]
        public int End;
        /// Array of alternative recognitions.
        [DataMember(Name = "alternatives")]
        public string[] Alternatives;
    }

    /// <summary>
    /// Defines a final result.
    /// </summary>
    [DataContract]
    public class FinalResultMessage
    {
        /// Message type identifier.
        [DataMember(Name = "type")]
        public string Type = "final";
        /// Partial result "major" identifier.
        [DataMember(Name = "id")]
        public string Id;
        /// Recognized text.
        [DataMember(Name = "recognition")]
        public string Recognition;
        /// Alternative recognitions.
        [DataMember(Name = "spans", EmitDefaultValue = false)]
        public Span[] Spans;
        /// Translation of the recognized text.
        [DataMember(Name = "translation", EmitDefaultValue = false)]
        public string Translation;
    }

    //
    // Messages from Client to Server. 
    //

    /// <summary>
    /// Defines a request to update features of an existing session.
    /// </summary>
    [DataContract]
    public class UpdateFeaturesMessage
    {
        /// Message type identifier.
        [DataMember(Name = "type")]
        public string Type = "updateFeatures";
        /// True if partial results should be provided to the client after the update.
        [DataMember(Name = "partial")]
        public bool SendPartialResults;
        /// True if text-to-speech should be provided to the client after the update.
        [DataMember(Name = "texttospeech")]
        public string SendTextToSpeech;
        /// Maximum number of alternative recognitions returned to the client after the update.
        [DataMember(Name = "max-alternatives", EmitDefaultValue = false)]
        public int MaxAlternatives;
    }

}
