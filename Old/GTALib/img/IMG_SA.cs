using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IMG_SA { 
    public void loadImg(IMG image)
    {
        List<IMG_Item> items = new List<IMG_Item>();

        ReadFunctions rf = new ReadFunctions();
        rf.openFile(image.getFileName());

        int itemCount = 0;

        // Message.displayMsgHigh("Ver2: " + rf.readChar() + rf.readChar() + rf.readChar() + rf.readChar());
        itemCount = rf.readInt();
        image.itemCount = itemCount;
        // Message.displayMsgHigh("Item Count: " + itemCount);

        for (int curItem = 0; curItem < itemCount; curItem++)
        {
            IMG_Item item = new IMG_Item();
            int itemOffset = rf.readInt() * 2048;
            int itemSize = rf.readInt() * 2048;
            String itemName = rf.readNullTerminatedString(24);
            int itemType = Utils.getFileType(itemName, image);
            // Message.displayMsgHigh("Offset: " + Utils.getHexString(itemOffset));
            // Message.displayMsgHigh("Size: " + itemSize + " bytes");
            // Message.displayMsgHigh("Name: " + itemName);
            // Message.displayMsgHigh("Type: " + itemType);
            item.setOffset(itemOffset);
            item.setName(itemName);
            item.setSize(itemSize);
            item.setType(itemType);
            items.Add(item);
        }

        if (items.Count > 0)
            image.setItems(items);
        else
            image.setItems(null);
    }

    /* private void updateCounter(int itemType){ switch(itemType){ case 0: //modelCount++; break; } } */

}
