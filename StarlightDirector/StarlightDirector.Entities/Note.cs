using System;
using System.Diagnostics;
using System.Windows;
using DereTore.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StarlightDirector.Entities {
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public sealed class Note : DependencyObject, IComparable<Note> {

        public event EventHandler<EventArgs> ExtraParamsChanged;

        [JsonProperty]
        public int ID { get; private set; }

        [JsonIgnore]
        public double HitTiming => Bar.TimeAtGrid(IndexInGrid);

        private int _indexInGrid;

        // "PositionInGrid" was the first name of this property used in serialization.
        [JsonProperty("positionInGrid")]
        public int IndexInGrid {
            get { return _indexInGrid; }
            set {
                _indexInGrid = value;
                Bar?.SortNotes();
            }
        }

        public NoteType Type {
            get { return (NoteType)GetValue(TypeProperty); }
            internal set { SetValue(TypeProperty, value); }
        }

        [JsonProperty]
        public NotePosition StartPosition {
            get { return (NotePosition)GetValue(StartPositionProperty); }
            set { SetValue(StartPositionProperty, value); }
        }

        [JsonProperty]
        public NotePosition FinishPosition {
            get { return (NotePosition)GetValue(FinishPositionProperty); }
            set { SetValue(FinishPositionProperty, value); }
        }

        public int IndexInTrack => (int)FinishPosition - 1;

        [JsonProperty]
        public NoteFlickType FlickType {
            get { return (NoteFlickType)GetValue(FlickTypeProperty); }
            set { SetValue(FlickTypeProperty, value); }
        }

        public bool IsSync {
            get { return (bool)GetValue(IsSyncProperty); }
            private set { SetValue(IsSyncProperty, value); }
        }

        public Bar Bar { get; internal set; }

        public bool IsFlick {
            get { return (bool)GetValue(IsFlickProperty); }
            private set { SetValue(IsFlickProperty, value); }
        }

        [JsonProperty("prevFlickNoteID")]
        public int PrevFlickOrSlideNoteID { get; internal set; }

        public Note PrevConnectNote {
            get { return _prevConnectNote; }
            internal set { SetPrevConnectNoteInternal(value); }
        }

        public bool HasPrevConnect => PrevConnectNote != null;

        [JsonProperty("nextFlickNoteID")]
        public int NextFlickOrSlideNoteID { get; internal set; }

        public Note NextConnectNote {
            get { return _nextConnectNote; }
            internal set { SetNextConnectNoteInternal(value); }
        }

        public bool HasNextConnect => NextConnectNote != null;

        [Obsolete("This property is provided for forward compatibility only.")]
        [JsonProperty]
        public int SyncTargetID {
            get {
                // For legacy versions that generate sync connection from this field
                // Connect the first note and the last note of sync groups
                if (HasPrevSync) {
                    if (HasNextSync) {
                        return EntityID.Invalid;
                    }
                    var final = PrevSyncTarget;
                    while (final.HasPrevSync) {
                        final = final.PrevSyncTarget;
                    }
                    return final.ID;
                } else {
                    if (!HasNextSync) {
                        return EntityID.Invalid;
                    }
                    var final = NextSyncTarget;
                    while (final.HasNextSync) {
                        final = final.NextSyncTarget;
                    }
                    return final.ID;
                }
            }
        }

        public Note PrevSyncTarget {
            get { return _prevSyncTarget; }
            internal set { SetPrevSyncTargetInternal(value); }
        }

        public Note NextSyncTarget {
            get { return _nextSyncTarget; }
            internal set { SetNextSyncTargetInternal(value); }
        }

        public bool HasPrevSync => PrevSyncTarget != null;

        public bool HasNextSync => NextSyncTarget != null;

        public bool IsHold {
            get { return (bool)GetValue(IsHoldProperty); }
            private set { SetValue(IsHoldProperty, value); }
        }

        public bool IsHoldStart => Type == NoteType.Hold && NextConnectNote != null;

        public bool IsHoldEnd => PrevConnectNote?.IsHoldStart ?? false;

        [JsonProperty]
        public int HoldTargetID { get; internal set; }
        
        public bool IsSlide {
            get { return (bool)GetValue(IsSlideProperty); }
            private set { SetValue(IsSlideProperty, value); }
        }

        public bool IsSlideStart => Type == NoteType.Slide && !HasPrevConnect && HasNextConnect;

        public bool IsSlideContinuation => Type == NoteType.Slide && HasPrevConnect && HasNextConnect && PrevConnectNote.IsSlide && NextConnectNote.IsSlide;

        public bool IsSlideEnd => Type == NoteType.Slide && !HasNextConnect && HasPrevConnect;

        public bool IsTap {
            get { return (bool)GetValue(IsTapProperty); }
            private set { SetValue(IsTapProperty, value); }
        }

        public NoteRelation NextConnectRelation {
            get {
                if (NextConnectNote == null) {
                    return NoteRelation.None;
                } else if (IsFlick) {
                    // A note can be flick begin and hold/slide end at the same time
                    // Thus IsFlick should be checked first
                    return NoteRelation.Flick;
                } else if (IsHold) {
                    return NoteRelation.Hold;
                } else if (IsSlide) {
                    return NoteRelation.Slide;
                } else {
                    return NoteRelation.None;
                }
            }
        }

        public NoteRelation PrevConnectRelation => PrevConnectNote?.NextConnectRelation ?? NoteRelation.None;

        public bool IsGamingNote => IsTypeGaming(Type);

        public bool IsSpecialNote => IsTypeSpecial(Type);

        public NoteExtraParams ExtraParams {
            get { return (NoteExtraParams)GetValue(ExtraParamsProperty); }
            set { SetValue(ExtraParamsProperty, value); }
        }

        public bool ShouldBeRenderedAsHold {
            get { return (bool)GetValue(ShouldBeRenderedAsHoldProperty); }
            private set { SetValue(ShouldBeRenderedAsHoldProperty, value); }
        }

        public bool ShouldBeRenderedAsSlide {
            get { return (bool)GetValue(ShouldBeRenderedAsSlideProperty); }
            private set { SetValue(ShouldBeRenderedAsSlideProperty, value); }
        }

        public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(NoteType), typeof(Note),
            new PropertyMetadata(NoteType.Invalid, OnTypeChanged));

        public static readonly DependencyProperty FlickTypeProperty = DependencyProperty.Register(nameof(FlickType), typeof(NoteFlickType), typeof(Note),
            new PropertyMetadata(NoteFlickType.Tap, OnFlickTypeChanged));

        public static readonly DependencyProperty IsSyncProperty = DependencyProperty.Register(nameof(IsSync), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsFlickProperty = DependencyProperty.Register(nameof(IsFlick), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsHoldProperty = DependencyProperty.Register(nameof(IsHold), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsSlideProperty = DependencyProperty.Register(nameof(IsSlide), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsTapProperty = DependencyProperty.Register(nameof(IsTap), typeof(bool), typeof(Note),
            new PropertyMetadata(true));

        public static readonly DependencyProperty StartPositionProperty = DependencyProperty.Register(nameof(StartPosition), typeof(NotePosition), typeof(Note),
            new PropertyMetadata(NotePosition.Nowhere));

        public static readonly DependencyProperty FinishPositionProperty = DependencyProperty.Register(nameof(FinishPosition), typeof(NotePosition), typeof(Note),
            new PropertyMetadata(NotePosition.Nowhere));

        public static readonly DependencyProperty ExtraParamsProperty = DependencyProperty.Register(nameof(ExtraParams), typeof(NoteExtraParams), typeof(Note),
            new PropertyMetadata(null, OnExtraParamsChanged));

        public static readonly DependencyProperty ShouldBeRenderedAsHoldProperty = DependencyProperty.Register(nameof(ShouldBeRenderedAsHold), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly DependencyProperty ShouldBeRenderedAsSlideProperty = DependencyProperty.Register(nameof(ShouldBeRenderedAsSlide), typeof(bool), typeof(Note),
            new PropertyMetadata(false));

        public static readonly Comparison<Note> TimingThenPositionComparison = (x, y) => {
            var r = TimingComparison(x, y);
            return r == 0 ? TrackPositionComparison(x, y) : r;
        };

        public static readonly Comparison<Note> TimingComparison = (x, y) => {
            if (x == null) {
                throw new ArgumentNullException(nameof(x));
            }
            if (y == null) {
                throw new ArgumentNullException(nameof(y));
            }
            if (x.Equals(y)) {
                return 0;
            }
            if (x.Bar != y.Bar) {
                return x.Bar.Index.CompareTo(y.Bar.Index);
            }
            var r = x.IndexInGrid.CompareTo(y.IndexInGrid);
            if (r == 0 && x.Type != y.Type && (x.Type == NoteType.VariantBpm || y.Type == NoteType.VariantBpm)) {
                // The Variant BPM note is always placed at the end on the same grid line.
                return x.Type == NoteType.VariantBpm ? 1 : -1;
            } else {
                return r;
            }
        };

        public int CompareTo(Note other) {
            if (other == null) {
                throw new ArgumentNullException(nameof(other));
            }
            return Equals(other) ? 0 : TimingComparison(this, other);
        }

        public static readonly Comparison<Note> TrackPositionComparison = (n1, n2) => ((int)n1.FinishPosition).CompareTo((int)n2.FinishPosition);

        public static bool operator >(Note left, Note right) {
            return TimingComparison(left, right) > 0;
        }

        public static bool operator <(Note left, Note right) {
            return TimingComparison(left, right) < 0;
        }

        public static bool IsTypeGaming(NoteType type) {
            return type == NoteType.TapOrFlick || type == NoteType.Hold || type == NoteType.Slide;
        }

        public static bool IsTypeSpecial(NoteType type) {
            return type == NoteType.VariantBpm;
        }

        public static void ConnectSync(Note first, Note second) {
            /*
             * Before:
             *     ... <==>    first    <==> first_next   <==> ...
             *     ... <==> second_prev <==>   second     <==> ...
             *
             * After:
             *                               first_next   <==> ...
             *     ... <==>    first    <==>   second     <==> ...
             *     ... <==> second_prev
             */
            if (first == second) {
                throw new ArgumentException("A note should not be connected to itself", nameof(second));
            } else if (first?.NextSyncTarget == second && second?.PrevSyncTarget == first) {
                return;
            }
            first?.NextSyncTarget?.SetPrevSyncTargetInternal(null);
            second?.PrevSyncTarget?.SetNextSyncTargetInternal(null);
            first?.SetNextSyncTargetInternal(second);
            second?.SetPrevSyncTargetInternal(first);
        }

        public void RemoveSync() {
            /*
             * Before:
             *     ... <==> prev <==> this <==> next <==> ...
             *
             * After:
             *     ... <==> prev <============> next <==> ...
             *                        this
             */
            PrevSyncTarget?.SetNextSyncTargetInternal(NextSyncTarget);
            NextSyncTarget?.SetPrevSyncTargetInternal(PrevSyncTarget);
            SetPrevSyncTargetInternal(null);
            SetNextSyncTargetInternal(null);
        }

        public void FixSync() {
            if (!IsGamingNote) {
                return;
            }
            RemoveSync();
            Note prev = null;
            Note next = null;
            foreach (var note in Bar.Notes) {
                if (note == this) {
                    continue;
                }
                if (!note.IsGamingNote) {
                    continue;
                }
                if (note.IndexInGrid == IndexInGrid) {
                    if (note.IndexInTrack < IndexInTrack) {
                        if (prev == null || prev.IndexInTrack < note.IndexInTrack) {
                            prev = note;
                        }
                    } else {
                        if (next == null || note.IndexInTrack < next.IndexInTrack) {
                            next = note;
                        }
                    }
                }
            }
            ConnectSync(prev, this);
            ConnectSync(this, next);
        }

        public static void ConnectFlick(Note first, Note second) {
            if (first == null) {
                second?.ResetPrevConnection();
                return;
            }
            if (second == null) {
                first?.ResetNextConnection();
                return;
            }
            if (first.IsFlick && first.NextConnectNote == second) {
                return;
            }
            first.ResetNextConnection();
            second.ResetPrevConnection();
            // Set first
            first.Type = NoteType.TapOrFlick;
            first.NextConnectNote = second;
            // Set second
            if (!(second.IsTap || second.IsFlick)) {
                second.ResetNextConnection();
            }
            second.Type = NoteType.TapOrFlick;
            second.PrevConnectNote = first;
        }

        public static void ConnectHold(Note first, Note second) {
            if (first == null) {
                second?.ResetPrevConnection();
                return;
            }
            if (second == null) {
                first?.ResetNextConnection();
                return;
            }
            if (first.IsHoldStart && first.NextConnectNote == second) {
                return;
            }
            first.ResetNextConnection();
            second.ResetPrevConnection();
            // Set first
            // There shouldn't be pre-connection on hold start notes
            first.ResetPrevConnection();
            first.Type = NoteType.Hold;
            first.NextConnectNote = second;
            // Set second
            // Flick is the only post-connection type allowed on hold end notes
            if (!(second.IsTap || second.IsFlick)) {
                second.ResetNextConnection();
            }
            // Only the former of the hold pair is considered as a hold note. The other is a tap or flick note.
            second.Type = NoteType.TapOrFlick;
            second.PrevConnectNote = first;
        }

        public static void ConnectSlide(Note first, Note second) {
            if (first == null) {
                second?.ResetPrevConnection();
                return;
            }
            if (second == null) {
                first?.ResetNextConnection();
                return;
            }
            if (first.IsSlide && first.NextConnectNote == second) {
                return;
            }
            first.ResetNextConnection();
            second.ResetPrevConnection();
            // Set first
            if (!(first.IsTap || first.IsSlide)) {
                first.ResetPrevConnection();
            }
            first.Type = NoteType.Slide;
            first.NextConnectNote = second;
            // Set second
            if (!(second.IsTap || second.IsSlide || second.IsFlick)) {
                second.ResetNextConnection();
            }
            second.Type = NoteType.Slide;
            second.PrevConnectNote = first;
        }

        internal int GroupID { get; set; }

        [JsonConstructor]
        internal Note(int id, Bar bar) {
            ID = id;
            Bar = bar;
            IndexInGrid = 0;
            Type = NoteType.TapOrFlick;
            StartPosition = NotePosition.Nowhere;
            FinishPosition = NotePosition.Nowhere;
            FlickType = NoteFlickType.Tap;
        }

        internal void ResetPrevConnection() {
            if (PrevConnectNote == null) {
                return;
            }
            Type = NextConnectNote?.Type ?? NoteType.TapOrFlick;
            // Reset PrevConnectNote first, otherwise calling ResetNextConnection will lead to infinite recursion
            var oldPrevConnectNote = PrevConnectNote;
            PrevConnectNote = null;
            oldPrevConnectNote.ResetNextConnection();
        }

        internal void ResetNextConnection() {
            if (NextConnectNote == null) {
                return;
            }
            Type = PrevConnectNote?.Type ?? NoteType.TapOrFlick;
            // Reset NextConnectNote first, otherwise calling ResetPrevConnection will lead to infinite recursion
            var oldNextConnectNote = NextConnectNote;
            NextConnectNote = null;
            oldNextConnectNote.ResetPrevConnection();
        }

        internal void ResetConnection() {
            ResetPrevConnection();
            ResetNextConnection();
        }

        internal void SetSpecialType(NoteType type) {
            switch (type) {
                case NoteType.VariantBpm:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
            Type = type;
        }

        // Why is ugly functions like this even exist?
        internal void SetIndexInGridWithoutSorting(int newIndex) {
            _indexInGrid = newIndex;
        }

        private static void OnTypeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
            var note = (Note)obj;
            note.UpdateNoteType();
            note.UpdateFlickType();
            note.PrevConnectNote?.UpdateFlickType();
            // IsHold and IsTap are dependent on PrevConnectNote
            note.NextConnectNote?.UpdateNoteType();
            note.NextConnectNote?.UpdateFlickType();
            note.UpdateNextConnectID();
            note.NextConnectNote?.UpdatePrevConnectID();
        }

        private static void OnFlickTypeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
            var note = (Note)obj;
            note.UpdateNoteType();
        }

        private static void OnExtraParamsChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
            var note = (Note)obj;
            var oldParams = (NoteExtraParams)e.OldValue;
            var newParams = (NoteExtraParams)e.NewValue;
            if (oldParams != null) {
                oldParams.ParamsChanged -= note.ExtraParams_ParamsChanged;
            }
            if (newParams != null) {
                newParams.ParamsChanged += note.ExtraParams_ParamsChanged;
            }
        }

        private void UpdateNoteType() {
            IsFlick = IsFlickInternal();
            IsTap = IsTapInternal();
            IsSlide = IsSlideInternal();
            IsHold = IsHoldInternal();
            ShouldBeRenderedAsHold = !IsFlick && IsHold;
            ShouldBeRenderedAsSlide = !IsFlick && IsSlide;
        }

        private void UpdateFlickType() {
            var n1 = PrevConnectNote;
            var n2 = NextConnectNote;
            // Currently there isn't an example of quick 'Z' turn appeared in CGSS (as shown below),
            // so the following completion method is good enough.
            //     -->
            //      ^
            //       \
            //      -->

            /* How should we set FlickType? (0=Tap, 1=Flick, X=Don't care, Z=Old value)
            Prev\Curr   TapOrFlick  Hold    Slide   Curr/Next
            (null)          0       X       X       (null)
            (null)          1       0       X       TapOrFlick
            (null)          X       0       X       Hold
            (null)          X       X       0       Slide
            TapOrFlick      1       X       X       (null)
            TapOrFlick      1       X       X       TapOrFlick
            TapOrFlick      X       X       X       Hold
            TapOrFlick      X       X       X       Slide
            Hold            Z       Z       X       (null)
            Hold            1       1       X       TapOrFlick
            Hold            X       X       X       Hold
            Hold            X       X       X       Slide
            Slide           1       X       0       (null)
            Slide           1       X       1       TapOrFlick
            Slide           X       X       X       Hold
            Slide           X       X       0       Slide
            Prev/Curr   TapOrFlick  Hold    Slide   Curr\Next
            */
            if (n2 != null) {
                var flickType2 = n2.FinishPosition > FinishPosition ? NoteFlickType.FlickRight : NoteFlickType.FlickLeft;
                if (n1 != null) {
                    if (n2.Type == NoteType.TapOrFlick) {
                        FlickType = flickType2;
                    } else {
                        FlickType = NoteFlickType.Tap;
                    }
                } else {
                    if (Type == NoteType.TapOrFlick) {
                        FlickType = flickType2;
                    } else {
                        FlickType = NoteFlickType.Tap;
                    }
                }
            } else if (n1 != null) {
                var flickType1 = n1.FinishPosition > FinishPosition ? NoteFlickType.FlickLeft : NoteFlickType.FlickRight;
                if (n1.Type == NoteType.Hold) {
                    return;
                } else if (Type == NoteType.TapOrFlick) {
                    FlickType = flickType1;
                } else {
                    FlickType = NoteFlickType.Tap;
                }
            } else {
                FlickType = NoteFlickType.Tap;
            }
        }

        private bool IsFlickInternal() => FlickType == NoteFlickType.FlickLeft || FlickType == NoteFlickType.FlickRight;

        private bool IsTapInternal() => Type == NoteType.TapOrFlick && FlickType == NoteFlickType.Tap && PrevConnectNote?.Type != NoteType.Hold;

        private bool IsSlideInternal() => Type == NoteType.Slide;

        private bool IsHoldInternal() => Type == NoteType.Hold || PrevConnectNote?.Type == NoteType.Hold;

        private void ExtraParams_ParamsChanged(object sender, EventArgs e) {
            // if we a BPM note is changed, inform the Bar to update its timings
            Bar?.UpdateTimingsChain();
            ExtraParamsChanged.Raise(sender, e);
        }

        private void SetPrevSyncTargetInternal(Note prev) {
            _prevSyncTarget = prev;
            IsSync = _prevSyncTarget != null || _nextSyncTarget != null;
        }

        private void SetNextSyncTargetInternal(Note next) {
            _nextSyncTarget = next;
            IsSync = _prevSyncTarget != null || _nextSyncTarget != null;
        }

        private void SetPrevConnectNoteInternal(Note prev) {
            if (prev == PrevConnectNote) {
                return;
            }
            _prevConnectNote = prev;
            // IsHold and IsTap are dependent on PrevConnectNote
            UpdateNoteType();
            UpdateFlickType();
            UpdatePrevConnectID();
        }

        private void SetNextConnectNoteInternal(Note next) {
            if (next == NextConnectNote) {
                return;
            }
            _nextConnectNote = next;
            UpdateFlickType();
            UpdateNextConnectID();
        }

        private void UpdatePrevConnectID() {
            var id = PrevConnectNote?.ID ?? EntityID.Invalid;
            if (IsHoldEnd) {
                HoldTargetID = id;
                PrevFlickOrSlideNoteID = EntityID.Invalid;
            } else {
                HoldTargetID = EntityID.Invalid;
                PrevFlickOrSlideNoteID = id;
            }
        }

        private void UpdateNextConnectID() {
            var id = NextConnectNote?.ID ?? EntityID.Invalid;
            if (IsHoldStart) {
                HoldTargetID = id;
                NextFlickOrSlideNoteID = EntityID.Invalid;
            } else {
                HoldTargetID = EntityID.Invalid;
                NextFlickOrSlideNoteID = id;
            }
        }

        private Note _prevConnectNote;
        private Note _nextConnectNote;
        private Note _prevSyncTarget;
        private Note _nextSyncTarget;

    }
}
