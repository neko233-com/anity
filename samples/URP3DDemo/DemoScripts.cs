using System.Collections;
using UnityEngine;

namespace Anity.Demos.URP3D
{
    public class Rotator : MonoBehaviour
    {
        public Vector3 rotationSpeed = new Vector3(0, 90, 0);
        public bool _updateCalled;
        protected override void Update()
        {
            _updateCalled = true;
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }

    public class PhysicsResponder : MonoBehaviour
    {
        public int _collisionCount;
        public int _triggerCount;
        protected override void OnCollisionEnter(Collision c) { _collisionCount++; }
        protected override void OnTriggerEnter(Collider c) { _triggerCount++; }
        public void AddImpulse() { var rb = GetComponent<Rigidbody>(); if (rb != null) rb.AddForce(Vector3.up * 5, ForceMode.Impulse); }
    }

    public class UIButtonHandler : MonoBehaviour
    {
        public static int _clickCount;
        public void OnRedClicked() { _clickCount++; DemoScene.SetAllObjectColor(Color.red); }
        public void OnGreenClicked() { _clickCount++; DemoScene.SetAllObjectColor(Color.green); }
        public void OnBlueClicked() { _clickCount++; DemoScene.SetAllObjectColor(Color.blue); }
    }

    public class CoroutineTester : MonoBehaviour
    {
        public int _coroutineTicks;
        public bool _coroutineComplete;
        IEnumerator TestCoroutine()
        {
            _coroutineTicks = 0;
            yield return null;
            _coroutineTicks++;
            yield return new WaitForSeconds(0.5f);
            _coroutineTicks++;
            _coroutineComplete = true;
        }
        protected override void Start() { StartCoroutine(TestCoroutine()); }
    }

    public class InvokeTester : MonoBehaviour
    {
        public int _invokeCount;
        protected override void Start() { InvokeRepeating("Tick", 0.1f, 0.1f); }
        void Tick() { _invokeCount++; }
    }
}
