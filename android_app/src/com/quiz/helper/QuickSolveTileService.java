package com.quiz.helper;

import android.service.quicksettings.TileService;

public class QuickSolveTileService extends TileService {
    @Override
    public void onClick() {
        super.onClick();
        ScreenReaderService reader = ScreenReaderService.instance;
        if (reader != null) {
            reader.triggerSolveDelayed(350);
        }
    }
}
