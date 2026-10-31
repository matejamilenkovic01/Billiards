// iOS haptics for HapticsService (v4): impact feedback in three strengths and a selection tick.
// style: 0 = light, 1 = medium, 2 = heavy, 3 = selection (the faintest tick).
#import <UIKit/UIKit.h>

extern "C" void BilliardsHapticImpact(int style)
{
    if (@available(iOS 10.0, *))
    {
        if (style == 3)
        {
            UISelectionFeedbackGenerator *selection = [[UISelectionFeedbackGenerator alloc] init];
            [selection selectionChanged];
            return;
        }
        UIImpactFeedbackStyle impact = style <= 0 ? UIImpactFeedbackStyleLight : style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:impact];
        [generator impactOccurred];
    }
}
