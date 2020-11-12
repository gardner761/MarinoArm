import cv2

cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)
tracker = cv2.TrackerCSRT_create()
success, img = cap.read()
bbox = cv2.selectROI("Tracking", img, False)
tracker.init(img, bbox) #draw the initial bounding box with the object of interest

def drawBox(img,bbox):
    x,y,w,h = int(bbox[0]),int(bbox[1]),int(bbox[2]),int(bbox[3])
    cv2.rectangle(img,(x,y),((x+w),(y+h)),(255,0,255),3,1)
    cv2.putText(img, "Tracking", (75, 75), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0))


while True:
    success, img = cap.read()
    success, bbox = tracker.update(img) #searches for the object of interest and outputs the bounding box params

    if success:
        drawBox(img, bbox)
    else:
        pass
    cv2.imshow("Tracking",img)
    if cv2.waitKey(1) & 0xff == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()