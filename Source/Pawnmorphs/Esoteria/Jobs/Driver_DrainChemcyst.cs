﻿using Verse;

namespace Pawnmorph.Jobs
{
    class Driver_DrainChemcyst : Driver_ProduceThing
    {
        public override void Produce()
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MutationsDefOf.EtherChemfuelUdder);
            var comp = hediff?.TryGetComp<HediffComp_Production>();

            comp?.Produce();
        }
    }
}
