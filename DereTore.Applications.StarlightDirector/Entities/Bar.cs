﻿using DereTore.Applications.StarlightDirector.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DereTore.Applications.StarlightDirector.Entities {
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public sealed class Bar {

        public Note AddNote() {
            var id = MathHelper.NextRandomPositiveInt32();
            while (NoteIDs.ExistingIDs.Contains(id)) {
                id = MathHelper.NextRandomPositiveInt32();
            }
            var note = new Note(id, this);
            Notes.Add(note);
            return note;
        }

        public InternalList<Note> Notes { get; }

        public BarParams Params { get; internal set; }

        public int Index { get; internal set; }

        [JsonIgnore]
        public Score Score { get; internal set; }

        [JsonConstructor]
        internal Bar(Score score, int index) {
            Score = score;
            Notes = new InternalList<Note>();
            Index = index;
        }

        public void SquashParams() {
            if (Params?.CanBeSquashed ?? false) {
                Params = null;
            }
        }

    }
}
