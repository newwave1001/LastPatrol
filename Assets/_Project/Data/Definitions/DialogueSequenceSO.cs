using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Data
{
    [CreateAssetMenu(fileName = "DS_NewSequence", menuName = "LastPatrol/Dialogue Sequence")]
    public class DialogueSequenceSO : ScriptableObject
    {
        public List<DialogueLineSO> lines = new List<DialogueLineSO>();
    }
}
