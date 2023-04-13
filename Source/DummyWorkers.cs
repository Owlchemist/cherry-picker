using Verse;
using RimWorld;
 
namespace CherryPicker
{
    //Any interactionDef that is removed uses this worker instead for the purpose of reducing the chance to 0
	public class InteractionWorker_Dummy : InteractionWorker
	{
		public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
		{
			return 0f;
		}
	}
}