# AI Enabled Digital Twin for Crack Evolution in Concrete Beams

Crack formation and propagation in reinforced concrete members remain critical concerns in structural health monitoring because most conventional inspection workflows are periodic, manual, and reactive. In many real-world situations, visible damage is identified only after structural deterioration has already progressed, while the internal stress state and the possible future direction of crack evolution remain difficult to interpret in a timely manner. This project addresses that gap by developing an AI-enabled digital twin for crack evolution in concrete beams. The main objective is to create a computationally efficient monitoring environment that preserves physical meaning while reducing the latency associated with traditional high-cost simulation workflows.

The proposed solution combines a physics-informed neural network with a Unity 3D digital twin interface. Instead of relying only on visual inspection or on purely data-driven prediction, the system uses structural parameters and physics-aware inference to produce interpretable outputs such as stress, strain, and a damage index. Those outputs are then mapped back into the digital twin so that the beam response can be visualized through heat maps and crack-sensitive regions. The beam model is segmented into 100 sections, representative locations are evaluated, and a threshold-based damage index is used to identify possible crack locations.

The current prototype demonstrates the feasibility of linking AI inference with a structural visualization environment for faster and clearer monitoring. Experimental evaluation compares the digital twin output against Abaqus finite element simulations, providing an engineering reference for the observed response patterns. Although future crack propagation prediction is not yet integrated into the deployed digital twin and automated crack-width or crack-length measurement is still pending, the project establishes a strong foundation for real-time AI-assisted damage visualization and future predictive monitoring in civil infrastructure.

#### Team

- e20016, Amarakeerthi H.K.K.G., [e20016@eng.pdn.ac.lk](mailto:e20016@eng.pdn.ac.lk)
- e20231, Madhura T.W.K.J., [e20231@eng.pdn.ac.lk](mailto:e20231@eng.pdn.ac.lk)
- e20404, Ukwaththa U.A.N.T., [e20404@eng.pdn.ac.lk](mailto:e20404@eng.pdn.ac.lk)

#### Supervisors

- Dr. Upul Jayasinghe, [upuljm@eng.pdn.ac.lk](mailto:upuljm@eng.pdn.ac.lk)
- Dr. J.A.S.C. Jayasinghe, [supunj@eng.pdn.ac.lk](mailto:supunj@eng.pdn.ac.lk)

#### Links

[//]: # ( NOTE: EDIT THIS LINKS WITH YOUR REPO DETAILS )

- [Project Page](http://ai-digital-twin-crack-monitoring-ve.vercel.app)
- [Department of Computer Engineering](http://www.ce.pdn.ac.lk/)
- [University of Peradeniya](https://eng.pdn.ac.lk/)



