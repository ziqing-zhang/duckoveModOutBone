using Duckov.Economy;
using Duckov.Utilities;

namespace TestMod
{
    public class CraftingFormulaManger
    {
        public CraftingFormula baseoutbone;
        public CraftingFormula repairoutbone;
        public  CraftingFormula armybone;
        public  CraftingFormula normalflybullet;
        public void InitializeFormulas()
        {
            baseoutbone = new CraftingFormula
            {
                id = "baseoutbone",
                result = new CraftingFormula.ItemEntry
                {
                    id=Somedata.OutboneId,
                    amount = 1
                },
                tags = new []{"WorkBenchAdvanced"},
                cost = new Cost()
                {
                    money = 0,
                    items = new Cost.ItemEntry[]{new Cost.ItemEntry{id=362, amount=1}}
                },
                unlockByDefault = true,
                lockInDemo =  false,
                hideInIndex = false,
                requirePerk = ""
            };
            repairoutbone = new CraftingFormula
            {
                id = "repairoutbone",
                result = new CraftingFormula.ItemEntry
                {
                    id=Somedata.RepairOutboonToolId,
                    amount = 1
                },
                tags = new []{"WorkBenchAdvanced"},
                cost = new Cost()
                {
                    money = 0,
                    items = new Cost.ItemEntry[]{new Cost.ItemEntry{id=362, amount=1}}
                },
                unlockByDefault = true,
                lockInDemo =  false,
                hideInIndex = false,
                requirePerk = ""
            };
            armybone = new CraftingFormula
            {
                id = "armybone",
                result = new CraftingFormula.ItemEntry
                {
                    id=Somedata.ArmyBoneId,
                    amount = 1
                },
                tags = new []{"WorkBenchAdvanced"},
                cost = new Cost()
                {
                    money = 0,
                    items = new Cost.ItemEntry[]{new Cost.ItemEntry{id=362, amount=1}}
                },
                unlockByDefault = true,
                lockInDemo =  false,
                hideInIndex = false,
                requirePerk = ""
            };
            normalflybullet = new CraftingFormula
            {
                id = "normalflybullet",
                result = new CraftingFormula.ItemEntry
                {
                    id=Somedata.NormalFlyBullet,
                    amount = 2
                },
                tags = new []{"WorkBenchAdvanced"},
                cost = new Cost()
                {
                    money = 0,
                    items = new Cost.ItemEntry[]{new Cost.ItemEntry{id=362, amount=1}}
                },
                unlockByDefault = true,
                lockInDemo =  false,
                hideInIndex = false,
                requirePerk = ""
            };
        }
    }
}