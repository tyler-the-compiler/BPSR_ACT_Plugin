using ACT_Plugin;
using Advanced_Combat_Tracker;
using System;

namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Encounter
    {
        BPSR_Line_Parser bpsrLineParser;
        bool isImport;
        DateTime time;

        public bool hasCombatStartedYet = false;
        private EncounterStateBase state = EncounterStateOutOfCombat.Instance();
        private DateTime? lastEncounterEvent;
        private TimeSpan timeout = new TimeSpan(0, 0, 6);
        public delegate void EncounterEvent(DateTime time);
        public event EncounterEvent BeforeEndCombat;

        public void EnterCombat(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            hasCombatStartedYet = true;
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.EnterCombat(this);
        }

        public void ExitCombat(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            hasCombatStartedYet = false;
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.ExitCombat(this);
        }
        public void CombatEvent(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.CombatEvent(this);
        }
        public void TimeoutCheck(bool isImport, DateTime time)
        {
            this.isImport = isImport;
            this.time = time;
            state.TimeoutCheck(this);
        }

        public void Reset()
        {
            state = EncounterStateOutOfCombat.Instance();
        }

        private class EncounterStateBase
        {
            protected virtual void EnterState() { }

            public virtual void EnterCombat(BPSR_Encounter enc) { }
            public virtual void ExitCombat(BPSR_Encounter enc) { }
            public virtual void CombatEvent(BPSR_Encounter enc) { }
            public virtual void TimeoutCheck(BPSR_Encounter enc) { }
            public virtual void Timeout() { }
        }

        private class EncounterStateOutOfCombat : EncounterStateBase
        {
            public static EncounterStateOutOfCombat Instance() { return instance; }
            private static EncounterStateOutOfCombat instance = new EncounterStateOutOfCombat();

            public override void EnterCombat(BPSR_Encounter enc)
            {

                if (ActGlobals.oFormActMain.InCombat)
                {
                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                }
                try
                {
                    if (ActGlobals.oFormActMain.SetEncounter(enc.time, enc.bpsrLineParser.source.Name, enc.bpsrLineParser.target.Name))
                    {
                        if (ActGlobals.oFormActMain.InCombat)
                        {
                            enc.state = EncounterStateInCombat.Instance();
                        }
                    }
                }
                catch { }
            }
        }

        private class EncounterStateInCombat : EncounterStateBase
        {
            public static EncounterStateInCombat Instance() { return instance; }
            private static EncounterStateInCombat instance = new EncounterStateInCombat();
            public override void ExitCombat(BPSR_Encounter enc)
            {
                if (ActGlobals.oFormActMain.InCombat)
                {
                    enc.lastEncounterEvent = enc.time;
                    enc.state = EncounterStateSoftCombat.Instance();

                    if (!enc.isImport)
                    {
                        //timer.Start();
                    }
                }
                else
                {
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }
        }

        private class EncounterStateSoftCombat : EncounterStateBase
        {
            public static EncounterStateSoftCombat Instance() { return instance; }
            private static EncounterStateSoftCombat instance = new EncounterStateSoftCombat();

            public override void EnterCombat(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                // timer.Stop();

                if (diff > enc.timeout)
                {
                    // Too long - new encounter.
                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    ActGlobals.oFormActMain.SetEncounter(enc.time, enc.bpsrLineParser.source.Name, enc.bpsrLineParser.target.Name);
                    if (ActGlobals.oFormActMain.InCombat)
                    {
                        enc.state = EncounterStateInCombat.Instance();
                    }
                    else
                    {
                        enc.state = EncounterStateOutOfCombat.Instance();
                    }
                }
                else
                {
                    enc.state = EncounterStateInCombat.Instance();
                }
            }

            public override void CombatEvent(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                // timer.Stop();

                if (diff > enc.timeout)
                {
                    // Too long - end encounter.

                    DateTime endTime = (DateTime)(enc.lastEncounterEvent + enc.timeout);
                    enc.BeforeEndCombat(endTime);

                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }

            public override void TimeoutCheck(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                if (diff > enc.timeout)
                {
                    // Too long - end encounter.
                    // timer.Stop();

                    DateTime endTime = (DateTime)(enc.lastEncounterEvent + enc.timeout);
                    enc.BeforeEndCombat(endTime);

                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }
        }
    }
}