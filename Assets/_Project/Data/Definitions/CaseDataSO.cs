using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Data
{
    [CreateAssetMenu(fileName = "C_NewCase", menuName = "LastPatrol/Case Data")]
    public class CaseDataSO : ScriptableObject
    {
        public string caseId;       // "CH1_NORDMAN", "SUB_A_MIA"
        public string caseTitleKR;
        public string caseTitleEN;
        public string caseAddressKR;
        public string caseAddressEN;

        [Header("Investigation Flow")]
        public List<ClueDataSO> clues = new List<ClueDataSO>();

        [Header("Dialogue")]
        public DialogueSequenceSO introDialogue;
        public DialogueSequenceSO outroDialogue;
    }
}
