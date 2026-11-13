//
// TCHaptics.mm - Native iOS haptic feedback for Tall Crawl
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

#if TARGET_OS_IOS
static UIImpactFeedbackGenerator *s_lightGenerator = nil;
#endif

extern "C" {

void _TC_PlayLightHaptic() {
#if TARGET_OS_IOS
    if (@available(iOS 10.0, *)) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (!s_lightGenerator) {
                s_lightGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            }
            [s_lightGenerator prepare];
            [s_lightGenerator impactOccurred];
        });
    }
#endif
}

}
