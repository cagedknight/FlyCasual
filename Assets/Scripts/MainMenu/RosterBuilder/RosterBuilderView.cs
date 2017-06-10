﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Players;

public static partial class RosterBuilder {

    //Ship Panels

    private static GameObject CreateShipPanel(PlayerNo playerNo)
    {
        GameObject prefab = GetShipPanelPefab();
        Transform parent = GetShipsPanel(playerNo);

        GameObject newPanel = MonoBehaviour.Instantiate(prefab, parent);
        newPanel.transform.localPosition = Vector3.zero;
        SubscribeControlButtons(playerNo, newPanel);

        return newPanel;
    }

    private static void GeneratePlayerShipConfigurations(PlayerNo playerNo)
    {
        foreach (Transform shipPanel in GetShipsPanel(playerNo))
        {
            if (shipPanel.name == "AddShipPanel") continue;
            string pilotNameFull = shipPanel.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>().captionText.text;
            string pilotNameId = AllPilots[pilotNameFull];

            List<string> upgradesList = new List<string>();

            foreach (Transform upgradePanel in shipPanel.transform.Find("GroupUpgrades"))
            {
                string upgradeName = upgradePanel.GetComponent<Dropdown>().captionText.text;
                if (AllUpgrades.ContainsKey(upgradeName))
                {
                    upgradesList.Add(AllUpgrades[upgradeName]);
                }
            }

            Global.AddShip(pilotNameId, upgradesList, playerNo);
        }
    }

    public static void CopyShip(PlayerNo playerNo, GameObject panel)
    {
        if (GetShipsCount(playerNo) < 8)
        {
            Transform parent = panel.transform.parent;
            GameObject newPanel = MonoBehaviour.Instantiate(panel, parent);

            SubscribeControlButtons(playerNo, newPanel);
            SubscribeShipDropdowns(playerNo, newPanel);

            UpdateShipCost(playerNo, panel);
            OrganizeAllShipsLists();
        }
        else
        {
            Messages.ShowError("You cannot have more than 8 ships");
        }
    }

    public static void RemoveShip(PlayerNo playerNo, GameObject panel)
    {
        MonoBehaviour.DestroyImmediate(panel);
        UpdateSquadCost(playerNo);
        OrganizeAllShipsLists();
    }

    //Events

    private static void SubscribeShipDropdowns(PlayerNo playerNo, GameObject panel)
    {
        Dropdown pilotDropdown = panel.transform.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>();
        pilotDropdown.onValueChanged.AddListener(delegate
        {
            OnPilotChanged(playerNo, panel);
        });
    }

    private static void SubscribeUpgradeDropdowns(PlayerNo playerNo, GameObject panel)
    {
        Dropdown upgradeDropdown = panel.transform.GetComponent<Dropdown>(); ;
        upgradeDropdown.onValueChanged.AddListener(delegate
        {
            OnUpgradeChanged(playerNo, panel.transform.parent.parent.gameObject);
        });
    }

    private static void SubscribeControlButtons(PlayerNo playerNo, GameObject panel)
    {
        panel.transform.Find("Panel").Find("RemoveButton").GetComponent<Button>().onClick.AddListener(delegate
        {
            RemoveShip(playerNo, panel);
        });

        panel.transform.Find("Panel").Find("CopyButton").GetComponent<Button>().onClick.AddListener(delegate
        {
            CopyShip(playerNo, panel);
        });
    }

    private static void OnPilotChanged(PlayerNo playerNo, GameObject panel)
    {
        UpdateUpgradePanels(playerNo, panel);
        UpdateShipCost(playerNo, panel);
    }

    private static void OnUpgradeChanged(PlayerNo playerNo, GameObject panel)
    {
        UpdateShipCost(playerNo, panel);
    }

    //Tooltips
    private static void AddPilotTooltip(GameObject panel)
    {
        Tooltips.AddTooltip(panel, GetPilotTooltipImage);
    }

    private static void AddUpgradeTooltip(GameObject panel)
    {
        Tooltips.AddTooltip(panel, GetUpgradeTooltipImage);
    }

    private static string GetPilotTooltipImage(GameObject panel)
    {
        string pilotKey = panel.GetComponent<Dropdown>().captionText.text;
        Ship.GenericShip ship = (Ship.GenericShip)Activator.CreateInstance(Type.GetType(AllPilots[pilotKey]));
        return ship.ImageUrl;
    }

    private static string GetUpgradeTooltipImage(GameObject panel)
    {
        string upgradeKey = panel.GetComponent<Dropdown>().captionText.text;
        if (AllUpgrades.ContainsKey(upgradeKey))
        {
            Upgrade.GenericUpgrade upgrade = (Upgrade.GenericUpgrade)Activator.CreateInstance(Type.GetType(AllUpgrades[upgradeKey]));
            return upgrade.ImageUrl;
        }
        return null;
    }

    //Set values to panels

    private static void SetShip(GameObject panel, PlayerNo playerNo)
    {
        List<string> results = new List<string>();
        foreach (var ships in AllShips)
        {
            Ship.GenericShip newShip = (Ship.GenericShip) Activator.CreateInstance(Type.GetType(ships.Value + "." + ships.Value.Substring(5)));
            if (newShip.faction == Global.GetPlayerFaction(playerNo))
            {
                results.Add(ships.Key);
            }
        }

        Dropdown shipDropdown = panel.transform.Find("GroupShip").Find("DropdownShip").GetComponent<Dropdown>();
        shipDropdown.ClearOptions();
        shipDropdown.AddOptions(results);

        SetPilot(playerNo, panel);

        UpdateShipCost(playerNo, panel);
    }

    private static void SetPilot(PlayerNo playerNo, GameObject panel)
    {
        string shipNameFull = panel.transform.Find("GroupShip").Find("DropdownShip").GetComponent<Dropdown>().captionText.text;
        string shipNameId = AllShips[shipNameFull];
        List<string> results = GetPilotsList(shipNameId);

        Dropdown pilotDropdown = panel.transform.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>();
        pilotDropdown.ClearOptions();
        pilotDropdown.AddOptions(results);

        SubscribeShipDropdowns(playerNo, panel);
        SetAvailableUpgrades(playerNo, panel, pilotDropdown.captionText.text);
        AddPilotTooltip(panel.transform.Find("GroupShip").Find("DropdownPilot").gameObject);

        OrganizeUpgradeLines(panel);
    }

    private static void UpdateUpgradePanels(PlayerNo playerNo, GameObject panel)
    {
        string pilotName = panel.transform.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>().captionText.text;
        string pilotId = AllPilots[pilotName];
        Ship.GenericShip ship = (Ship.GenericShip)Activator.CreateInstance(Type.GetType(pilotId));

        foreach (var slot in ship.BuiltInSlots)
        {
            if (panel.transform.Find("GroupUpgrades").Find("Upgrade" + slot.Key.ToString() + "Line") == null)
            {
                AddUpgradeLine(playerNo, panel, slot.Key.ToString());
            }
        }

        List<GameObject> toRemove = new List<GameObject>();
        foreach (Transform group in panel.transform.Find("GroupUpgrades"))
        {
            Type type = typeof(Upgrade.UpgradeSlot);
            string upgradeId = group.GetComponent<Dropdown>().options[0].text;
            upgradeId = upgradeId.Substring(upgradeId.IndexOf(':') + 2);
            Upgrade.UpgradeSlot slot = (Upgrade.UpgradeSlot)Enum.Parse(type, upgradeId);
            if (!ship.BuiltInSlots.ContainsKey(slot)) toRemove.Add(group.gameObject);
        }
        foreach (var group in toRemove)
        {
            MonoBehaviour.DestroyImmediate(group);
            OrganizeUpgradeLines(panel);
        }
    }

    private static void AddUpgradeLine(PlayerNo playerNo, GameObject panel, string upgradeId)
    {
        GameObject prefab = GameObject.Find("ScriptHolder").GetComponent<MainMenuScript>().UpgradeLinePrefab;
        Transform parent = panel.transform.Find("GroupUpgrades");

        GameObject newUpgradeLine = MonoBehaviour.Instantiate(prefab, parent);
        newUpgradeLine.transform.localPosition = Vector3.zero;
        newUpgradeLine.name = "Upgrade" + upgradeId + "Line";

        string emptySlot = "Empty Slot: " + upgradeId;
        List<string> emptySlotList = new List<string>() { emptySlot };
        newUpgradeLine.transform.GetComponent<Dropdown>().ClearOptions();
        newUpgradeLine.transform.GetComponent<Dropdown>().AddOptions(emptySlotList);

        Type type = typeof(Upgrade.UpgradeSlot);
        List<string> upgradeList = GetUpgrades((Upgrade.UpgradeSlot)Enum.Parse(type, upgradeId));
        newUpgradeLine.transform.GetComponent<Dropdown>().AddOptions(upgradeList);

        SubscribeUpgradeDropdowns(playerNo, newUpgradeLine);
        AddUpgradeTooltip(newUpgradeLine);

        OrganizeUpgradeLines(panel);
    }

    // Update Costs

    private static void UpdateShipCost(PlayerNo playerNo, GameObject panel)
    {
        int totalShipCost = 0;

        string pilotKey = panel.transform.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>().captionText.text;
        Ship.GenericShip shipContainer = (Ship.GenericShip)Activator.CreateInstance(Type.GetType(AllPilots[pilotKey]));
        totalShipCost += shipContainer.Cost;

        foreach (Transform upgradePanel in panel.transform.Find("GroupUpgrades"))
        {
            string upgradeName = upgradePanel.transform.GetComponent<Dropdown>().captionText.text;
            
            if (AllUpgrades.ContainsKey(upgradeName))
            {
                Upgrade.GenericUpgrade upgradeContainer = (Upgrade.GenericUpgrade)Activator.CreateInstance(System.Type.GetType(AllUpgrades[upgradeName]));
                totalShipCost += upgradeContainer.Cost;
            }
        }

        panel.transform.Find("Panel").Find("CostPanel").GetComponentInChildren<Text>().text = totalShipCost.ToString();
        UpdateSquadCost(playerNo);
    }

    private static void UpdateSquadCost(PlayerNo playerNo)
    {
        int squadCost = 0;
        

        foreach (Transform shipPanel in GetShipsPanel(playerNo))
        {
            if (shipPanel.name != "AddShipPanel")
            {
                squadCost += int.Parse(shipPanel.Find("Panel").Find("CostPanel").GetComponentInChildren<Text>().text);
            }
        }

        GetPlayerPanel(playerNo).Find("SquadCostPanel").Find("CostCurrent").GetComponent<Text>().text = squadCost.ToString();
        GetPlayerPanel(playerNo).Find("SquadCostPanel").Find("CostCurrent").GetComponent<Text>().color = (squadCost > 100) ? Color.red : Color.white;
    }

    //Get information from panels

    private static Faction GetPlayerFaction(PlayerNo playerNo)
    {
        int index = GetPlayerPanel(playerNo).Find("GroupFaction").Find("Dropdown").GetComponent<Dropdown>().value;
        switch (index)
        {
            case 0:
                return Faction.Rebels;
            case 1:
                return Faction.Empire;
        }
        return Faction.Empire;
    }

    private static Type GetPlayerType(PlayerNo playerNo)
    {
        int index = GetPlayerPanel(playerNo).Find("GroupPlayer").Find("Dropdown").GetComponent<Dropdown>().value;
        switch (index)
        {
            case 0: return typeof(HumanPlayer);
            case 1: return typeof(HotacAiPlayer);
        }
        return null;
    }

    private static int GetShipsCount(PlayerNo playerNo)
    {
        int result = 0;

        foreach (var panel in GetShipsPanel(playerNo))
        {
            result++;
        }
        result--;

        return result;
    }

    //Faction change

    private static void CheckPlayerFactonChange(PlayerNo playerNo)
    {
        Faction playerFaction = GetPlayerFaction(playerNo);

        List<Transform> playerShips = new List<Transform>();
        foreach (Transform shipPanel in GetShipsPanel(playerNo))
        {
            playerShips.Add(shipPanel);
        }

        foreach (Transform shipPanel in playerShips)
        {
            if (shipPanel.name == "AddShipPanel") continue;
            string pilotName = shipPanel.Find("GroupShip").Find("DropdownPilot").GetComponent<Dropdown>().captionText.text;
            Ship.GenericShip newPilot = (Ship.GenericShip)Activator.CreateInstance(Type.GetType(AllPilots[pilotName]));
            if (newPilot.faction != playerFaction) RemoveShip(playerNo, shipPanel.gameObject);
        }
    }

    //Get GameObjects

    private static GameObject GetShipPanelPefab()
    {
        return GameObject.Find("ScriptHolder").GetComponent<MainMenuScript>().RosterBuilderPrefab;
    }

    private static Transform GetPlayerPanel(PlayerNo playerNo)
    {
        return GameObject.Find("UI").transform.Find("Panel").Find("SquadBuilderPanel").Find("PlayersPanel").Find("Player" + Tools.PlayerToInt(playerNo) + "Panel");
    }

    private static Transform GetShipsPanel(PlayerNo playerNo)
    {
        return GameObject.Find("UI").transform.Find("Panel").Find("SquadBuilderPanel").Find("ShipsPanel").Find("Player" + Tools.PlayerToInt(playerNo) + "Ships").Find("Scroll View").Find("Viewport").Find("Content");
    }

    //Organization

    private static void OrganizeAllShipsLists()
    {
        OrganizeShipsList(PlayerNo.Player1);
        OrganizeShipsList(PlayerNo.Player2);
    }

    private static void OrganizeShipsList(PlayerNo playerNo)
    {
        float offset = 0;
        Transform addShipPanel = GameObject.Find("Global").transform;
        foreach (Transform shipPanel in GetShipsPanel(playerNo))
        {
            if (shipPanel.Find("AddShipButton") != null)
            {
                addShipPanel = shipPanel;
            }
            else
            {
                shipPanel.localPosition = new Vector3(shipPanel.localPosition.x, offset, shipPanel.localPosition.z);
                offset = offset - shipPanel.GetComponent<RectTransform>().sizeDelta.y - 30 - 5;
            }
        }

        addShipPanel.localPosition = new Vector3(addShipPanel.localPosition.x, offset, addShipPanel.localPosition.z);

        GetShipsPanel(playerNo).GetComponent<RectTransform>().sizeDelta = new Vector3(0, -offset + 40);
    }

    private static void OrganizeUpgradeLines(GameObject panel)
    {
        float offset = 0;
        int i = 0;
        foreach (Transform group in panel.transform.Find("GroupUpgrades"))
        {
            if (i%2 == 0)
            {
                group.localPosition = new Vector3(0, offset, group.localPosition.z);
            }
            else
            {
                group.localPosition = new Vector3(285, offset, group.localPosition.z);
                offset = offset - 30;
            }
            i++;
        }

        if (i % 2 == 1) offset = offset - 30;
        if (i == 0) offset = offset + 10;
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(580, -offset + 50);

        OrganizeAllShipsLists();
    }

}